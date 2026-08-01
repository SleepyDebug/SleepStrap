using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SleepStrap.Services
{
    // NVAPI DRS interop is based on the MIT-licensed NVIDIA Profile Inspector
    // source supplied with SleepStrap. See docs/licenses/NvidiaProfileInspector-LICENSE.txt.
    internal static class NvidiaProfileBlurService
    {
        private const string ProfileName = "Roblox VR";
        private const string MissingValue = "__SLEEPSTRAP_NVIDIA_SETTING_WAS_MISSING__";

        private const uint AaModeReplay = 0x10D48A85;
        private const uint AnisotropicMode = 0x10D2BB16;
        private const uint AnisotropicLevel = 0x101E61A9;
        private const uint LodBiasDx = 0x00738E8F;
        private const uint LodBiasOgl = 0x20403F79;
        private const uint NegativeLodBias = 0x0019BB68;

        private static readonly IReadOnlyDictionary<uint, uint> BlurValues =
            new Dictionary<uint, uint>
            {
                [AaModeReplay] = 0x00000008,      // AA replay mode: all
                [AnisotropicMode] = 0x00000001,  // User-defined / off
                [AnisotropicLevel] = 0x00000000, // Off (point)
                [LodBiasDx] = 0x00000018,        // +3.0000
                [LodBiasOgl] = 0x00000030,       // +3.0000
                [NegativeLodBias] = 0x00000000   // Allow
            };

        public static Dictionary<string, string> Enable()
        {
            using var api = new NvApi();
            IntPtr session = api.CreateSession();
            try
            {
                api.LoadSettings(session);
                IntPtr profile = api.FindProfile(session, ProfileName);
                var backup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach ((uint settingId, uint desiredValue) in BlurValues)
                {
                    uint? currentValue = api.ReadCurrentProfileDword(session, profile, settingId);
                    backup[settingId.ToString("X8")] = currentValue?.ToString() ?? MissingValue;
                    api.WriteDword(session, profile, settingId, desiredValue);
                }

                api.SaveSettings(session);
                return backup;
            }
            finally
            {
                api.DestroySession(session);
            }
        }

        public static void Disable(IReadOnlyDictionary<string, string> backup)
        {
            if (backup.Count == 0)
                throw new InvalidOperationException(
                    $"{App.ProjectName} has no NVIDIA profile backup to restore. The driver profile was not changed.");

            using var api = new NvApi();
            IntPtr session = api.CreateSession();
            try
            {
                api.LoadSettings(session);
                IntPtr profile = api.FindProfile(session, ProfileName);

                foreach (uint settingId in BlurValues.Keys)
                {
                    string key = settingId.ToString("X8");
                    if (!backup.TryGetValue(key, out string? previous))
                        continue;

                    if (String.Equals(previous, MissingValue, StringComparison.Ordinal))
                        api.DeleteSetting(session, profile, settingId);
                    else if (UInt32.TryParse(previous, out uint previousValue))
                        api.WriteDword(session, profile, settingId, previousValue);
                    else
                        throw new InvalidOperationException($"The saved NVIDIA setting backup for 0x{key} is invalid.");
                }

                api.SaveSettings(session);
            }
            finally
            {
                api.DestroySession(session);
            }
        }

        private enum NvStatus : int
        {
            Ok = 0,
            LibraryNotFound = -2,
            NvidiaDeviceNotFound = -6,
            SettingNotFound = -160,
            ProfileNotFound = -163,
            InvalidUserPrivilege = -137,
            AccessDenied = -175
        }

        private enum SettingType : int
        {
            Dword = 0
        }

        private enum SettingLocation : int
        {
            CurrentProfile = 0
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode, Size = 4100)]
        private struct SettingUnion
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4100)]
            public byte[] RawData;

            public static SettingUnion Empty() => new() { RawData = new byte[4100] };

            public static SettingUnion FromDword(uint value)
            {
                SettingUnion result = Empty();
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, result.RawData, 0, sizeof(uint));
                return result;
            }

            public readonly uint GetDword() =>
                RawData is { Length: >= 4 } ? BitConverter.ToUInt32(RawData, 0) : 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        private struct DrsSetting
        {
            public uint Version;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NvApi.UnicodeStringMax)]
            public string SettingName;

            public uint SettingId;
            public SettingType SettingType;
            public SettingLocation SettingLocation;
            public uint IsCurrentPredefined;
            public uint IsPredefinedValid;
            public SettingUnion PredefinedValue;
            public SettingUnion CurrentValue;

            public static DrsSetting Empty(uint version) => new()
            {
                Version = version,
                SettingName = String.Empty,
                PredefinedValue = SettingUnion.Empty(),
                CurrentValue = SettingUnion.Empty()
            };
        }

        private sealed class NvApi : IDisposable
        {
            public const int UnicodeStringMax = 2048;

            private const uint InitializeId = 0x0150E828;
            private const uint UnloadId = 0xD22BDD7E;
            private const uint CreateSessionId = 0x0694D52E;
            private const uint DestroySessionId = 0xDAD9CFF8;
            private const uint LoadSettingsId = 0x375DBD6B;
            private const uint SaveSettingsId = 0xFCBC7E14;
            private const uint FindProfileByNameId = 0x7E4A9A0B;
            private const uint SetSettingId = 0x8A2CF5F5;
            private const uint SetSettingFallbackId = 0x577DD202;
            private const uint GetSettingId = 0xEA99498D;
            private const uint GetSettingFallbackId = 0x73BF8338;
            private const uint DeleteSettingId = 0xD20D29DF;
            private const uint DeleteSettingFallbackId = 0xE4A26362;

            private readonly IntPtr _library;
            private readonly QueryInterfaceDelegate _queryInterface;
            private readonly InitializeDelegate _initialize;
            private readonly UnloadDelegate _unload;
            private readonly CreateSessionDelegate _createSession;
            private readonly DestroySessionDelegate _destroySession;
            private readonly LoadSettingsDelegate _loadSettings;
            private readonly SaveSettingsDelegate _saveSettings;
            private readonly FindProfileByNameDelegate _findProfileByName;
            private readonly SetSettingDelegate _setSetting;
            private readonly GetSettingDelegate _getSetting;
            private readonly DeleteSettingDelegate _deleteSetting;
            private bool _initialized;

            private static readonly uint DrsSettingVersion =
                (uint)(Marshal.SizeOf<DrsSetting>() | (1 << 16));

            public NvApi()
            {
                string libraryName = Environment.Is64BitProcess ? "nvapi64.dll" : "nvapi.dll";
                _library = LoadLibrary(libraryName);
                if (_library == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "The NVIDIA driver API is unavailable. This option requires an NVIDIA GPU and installed NVIDIA driver.");

                try
                {
                    IntPtr queryAddress = GetProcAddress(_library, "nvapi_QueryInterface");
                    if (queryAddress == IntPtr.Zero)
                        throw new InvalidOperationException("The installed NVIDIA driver does not expose NVAPI.");

                    _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryAddress);
                    _initialize = GetDelegate<InitializeDelegate>(InitializeId);
                    _unload = GetDelegate<UnloadDelegate>(UnloadId);
                    _createSession = GetDelegate<CreateSessionDelegate>(CreateSessionId);
                    _destroySession = GetDelegate<DestroySessionDelegate>(DestroySessionId);
                    _loadSettings = GetDelegate<LoadSettingsDelegate>(LoadSettingsId);
                    _saveSettings = GetDelegate<SaveSettingsDelegate>(SaveSettingsId);
                    _findProfileByName = GetDelegate<FindProfileByNameDelegate>(FindProfileByNameId);
                    _setSetting = GetDelegate<SetSettingDelegate>(SetSettingId, SetSettingFallbackId);
                    _getSetting = GetDelegate<GetSettingDelegate>(GetSettingId, GetSettingFallbackId);
                    _deleteSetting = GetDelegate<DeleteSettingDelegate>(DeleteSettingId, DeleteSettingFallbackId);

                    ThrowIfFailed("NvAPI_Initialize", _initialize());
                    _initialized = true;
                }
                catch
                {
                    FreeLibrary(_library);
                    throw;
                }
            }

            public IntPtr CreateSession()
            {
                IntPtr session = IntPtr.Zero;
                ThrowIfFailed("DRS_CreateSession", _createSession(ref session));
                return session;
            }

            public void DestroySession(IntPtr session)
            {
                if (session != IntPtr.Zero)
                    ThrowIfFailed("DRS_DestroySession", _destroySession(session));
            }

            public void LoadSettings(IntPtr session) =>
                ThrowIfFailed("DRS_LoadSettings", _loadSettings(session));

            public void SaveSettings(IntPtr session) =>
                ThrowIfFailed("DRS_SaveSettings", _saveSettings(session));

            public IntPtr FindProfile(IntPtr session, string profileName)
            {
                IntPtr profile = IntPtr.Zero;
                NvStatus status = _findProfileByName(
                    session,
                    new StringBuilder(profileName, UnicodeStringMax),
                    ref profile);

                if (status == NvStatus.ProfileNotFound || profile == IntPtr.Zero)
                    throw new InvalidOperationException(
                        $"The NVIDIA profile \"{profileName}\" was not found. Update or reinstall the NVIDIA driver profiles.");

                ThrowIfFailed("DRS_FindProfileByName", status);
                return profile;
            }

            public uint? ReadCurrentProfileDword(IntPtr session, IntPtr profile, uint settingId)
            {
                DrsSetting setting = DrsSetting.Empty(DrsSettingVersion);
                uint unknown = 0;
                NvStatus status = _getSetting(session, profile, settingId, ref setting, ref unknown);

                if (status == NvStatus.SettingNotFound ||
                    setting.SettingLocation != SettingLocation.CurrentProfile)
                {
                    return null;
                }

                ThrowIfFailed("DRS_GetSetting", status);
                return setting.CurrentValue.GetDword();
            }

            public void WriteDword(IntPtr session, IntPtr profile, uint settingId, uint value)
            {
                DrsSetting setting = DrsSetting.Empty(DrsSettingVersion);
                setting.SettingId = settingId;
                setting.SettingType = SettingType.Dword;
                setting.SettingLocation = SettingLocation.CurrentProfile;
                setting.CurrentValue = SettingUnion.FromDword(value);
                ThrowIfFailed("DRS_SetSetting", _setSetting(session, profile, ref setting, 0, 0));
            }

            public void DeleteSetting(IntPtr session, IntPtr profile, uint settingId)
            {
                NvStatus status = _deleteSetting(session, profile, settingId);
                if (status != NvStatus.SettingNotFound)
                    ThrowIfFailed("DRS_DeleteProfileSetting", status);
            }

            private T GetDelegate<T>(uint id, uint? fallbackId = null) where T : Delegate
            {
                IntPtr address = _queryInterface(id);
                if (address == IntPtr.Zero && fallbackId.HasValue)
                    address = _queryInterface(fallbackId.Value);
                if (address == IntPtr.Zero)
                    throw new InvalidOperationException($"The installed NVIDIA driver is missing NVAPI function 0x{id:X8}.");
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }

            private static void ThrowIfFailed(string operation, NvStatus status)
            {
                if (status == NvStatus.Ok)
                    return;

                string message = status switch
                {
                    NvStatus.InvalidUserPrivilege or NvStatus.AccessDenied =>
                        "NVIDIA denied the driver-profile change. Approve the administrator prompt and try again.",
                    NvStatus.NvidiaDeviceNotFound =>
                        "No supported NVIDIA GPU was found.",
                    _ => $"NVIDIA NVAPI returned {status} ({(int)status})."
                };
                throw new InvalidOperationException($"{operation} failed. {message}");
            }

            public void Dispose()
            {
                if (_initialized)
                {
                    _unload();
                    _initialized = false;
                }

                if (_library != IntPtr.Zero)
                    FreeLibrary(_library);
            }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate IntPtr QueryInterfaceDelegate(uint id);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus InitializeDelegate();

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus UnloadDelegate();

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus CreateSessionDelegate(ref IntPtr session);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus DestroySessionDelegate(IntPtr session);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus LoadSettingsDelegate(IntPtr session);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus SaveSettingsDelegate(IntPtr session);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus FindProfileByNameDelegate(
                IntPtr session,
                [MarshalAs(UnmanagedType.LPWStr, SizeConst = UnicodeStringMax)] StringBuilder profileName,
                ref IntPtr profile);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus SetSettingDelegate(
                IntPtr session,
                IntPtr profile,
                ref DrsSetting setting,
                uint unknown1,
                uint unknown2);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus GetSettingDelegate(
                IntPtr session,
                IntPtr profile,
                uint settingId,
                ref DrsSetting setting,
                ref uint unknown);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate NvStatus DeleteSettingDelegate(
                IntPtr session,
                IntPtr profile,
                uint settingId);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr LoadLibrary(string fileName);

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            private static extern IntPtr GetProcAddress(IntPtr module, string procedureName);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool FreeLibrary(IntPtr module);
        }
    }
}
