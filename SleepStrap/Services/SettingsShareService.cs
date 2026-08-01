using System.IO.Compression;

namespace SleepStrap.Services
{
    /// <summary>
    /// Creates compact, portable settings codes. The codes deliberately omit
    /// machine-specific paths, device IDs, restore backups, FastFlag snapshots,
    /// and user-imported skybox files. A selected built-in sky is still shared.
    /// </summary>
    internal static class SettingsShareService
    {
        private const string Prefix = "SBX1.";
        private const int MaxShareCodeLength = 16_384;
        private const int MaxDecompressedBytes = 32_768;

        private static readonly int[] ValidFpsLimits = { 0, 60, 120, 144, 165, 240, 360, 480, 9999 };
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public static string CreateCode(Settings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            string json = JsonSerializer.Serialize(SharedSettings.From(settings), SerializerOptions);
            byte[] compressed = Compress(Encoding.UTF8.GetBytes(json));
            return Prefix + ToBase64Url(compressed);
        }

        /// <summary>
        /// Validates and applies a code to the in-memory settings object. The caller
        /// saves it only after this succeeds, so an invalid code cannot leave a
        /// partial configuration behind.
        /// </summary>
        public static void ApplyCode(string shareCode, Settings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            SharedSettings profile = Parse(shareCode);
            profile.ApplyTo(settings);
        }

        public static bool TryApplyPortableFont(Settings settings)
        {
            string? fontName = settings.SelectedFontName;
            if (fontName is null)
                return true;

            try
            {
                if (fontName.Length == 0 || String.Equals(fontName, "Roblox Default", StringComparison.OrdinalIgnoreCase))
                {
                    FontModService.RestoreDefault();
                    return true;
                }

                FontChoice? choice = FontModService.GetAvailableFonts()
                    .FirstOrDefault(font => !font.IsDefault &&
                        String.Equals(font.DisplayName, fontName, StringComparison.OrdinalIgnoreCase));
                if (choice is null)
                    return false;

                FontModService.ApplyFont(choice);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("SettingsShareService::ApplyPortableFont", ex);
                return false;
            }
        }

        private static SharedSettings Parse(string rawCode)
        {
            string shareCode = Regex.Replace(rawCode ?? String.Empty, @"\s+", String.Empty);
            if (!shareCode.StartsWith(Prefix, StringComparison.Ordinal) || shareCode.Length > MaxShareCodeLength)
                throw new InvalidDataException("That is not a valid SleepBlox settings code.");

            byte[] compressed;
            try
            {
                compressed = FromBase64Url(shareCode[Prefix.Length..]);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("The settings code is damaged or incomplete.", ex);
            }

            string json;
            try
            {
                json = Encoding.UTF8.GetString(Decompress(compressed));
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("The settings code could not be unpacked.", ex);
            }

            SharedSettings? profile;
            try
            {
                profile = JsonSerializer.Deserialize<SharedSettings>(json, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("The settings code does not contain valid settings.", ex);
            }

            if (profile is null || profile.Version != 1)
                throw new InvalidDataException("This settings code was made by an unsupported SleepBlox version.");

            profile.Validate();
            return profile;
        }

        private static byte[] Compress(byte[] input)
        {
            using var destination = new MemoryStream();
            using (var compressor = new BrotliStream(destination, CompressionLevel.SmallestSize, leaveOpen: true))
                compressor.Write(input, 0, input.Length);
            return destination.ToArray();
        }

        private static byte[] Decompress(byte[] input)
        {
            using var source = new MemoryStream(input, writable: false);
            using var decompressor = new BrotliStream(source, CompressionMode.Decompress);
            using var destination = new MemoryStream();
            byte[] buffer = new byte[4096];

            int read;
            while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (destination.Length + read > MaxDecompressedBytes)
                    throw new InvalidDataException("That settings code is too large.");
                destination.Write(buffer, 0, read);
            }

            return destination.ToArray();
        }

        private static string ToBase64Url(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromBase64Url(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length > MaxShareCodeLength - Prefix.Length ||
                !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$"))
            {
                throw new FormatException();
            }

            string base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = (base64.Length % 4) switch
            {
                0 => base64,
                2 => base64 + "==",
                3 => base64 + "=",
                _ => throw new FormatException()
            };
            return Convert.FromBase64String(base64);
        }

        private sealed class SharedSettings
        {
            // Short JSON names plus Brotli make the code easy to paste in chat while
            // keeping this versioned, inspectable, and forward-compatible.
            [JsonPropertyName("v")]
            public int Version { get; set; } = 1;

            [JsonPropertyName("a")] public int? BootstrapperStyle { get; set; }
            [JsonPropertyName("b")] public int? BootstrapperIcon { get; set; }
            [JsonPropertyName("c")] public string? BootstrapperTitle { get; set; }
            [JsonPropertyName("d")] public int? Theme { get; set; }
            [JsonPropertyName("e")] public bool? MultiInstance { get; set; }
            [JsonPropertyName("f")] public bool? ConfirmLaunches { get; set; }
            [JsonPropertyName("g")] public string? Locale { get; set; }
            [JsonPropertyName("h")] public bool? ForceRobloxLanguage { get; set; }
            [JsonPropertyName("i")] public bool? UseFastFlagManager { get; set; }
            [JsonPropertyName("j")] public bool? SoftwareRender { get; set; }
            [JsonPropertyName("k")] public bool? Analytics { get; set; }
            [JsonPropertyName("l")] public bool? UpdateRoblox { get; set; }
            [JsonPropertyName("m")] public bool? BackgroundUpdates { get; set; }
            [JsonPropertyName("n")] public bool? CloseOnLaunch { get; set; }
            [JsonPropertyName("o")] public bool? OverrideLegacySettings { get; set; }
            [JsonPropertyName("p")] public int? Cleaner { get; set; }
            [JsonPropertyName("q")] public bool? ActivityTracking { get; set; }
            [JsonPropertyName("r")] public bool? DiscordRichPresence { get; set; }
            [JsonPropertyName("s")] public bool? HideRpcButtons { get; set; }
            [JsonPropertyName("t")] public bool? ShowAccount { get; set; }
            [JsonPropertyName("u")] public bool? ShowServerDetails { get; set; }
            [JsonPropertyName("w")] public bool? ShowServerUptime { get; set; }
            [JsonPropertyName("x")] public bool? DisableAppPatch { get; set; }
            [JsonPropertyName("y")] public bool? DarkTextures { get; set; }
            [JsonPropertyName("z")] public bool? RtxShine { get; set; }
            [JsonPropertyName("A")] public string? FontName { get; set; }
            [JsonPropertyName("B")] public string? FontColor { get; set; }
            // null means the sender has a local imported sky selected. Preserve the
            // recipient's own sky rather than transferring a broken reference.
            [JsonPropertyName("C")] public string? BuiltInSky { get; set; }
            [JsonPropertyName("D")] public List<string>? FavoriteSkyboxes { get; set; }
            [JsonPropertyName("E")] public int? StretchPercent { get; set; }
            [JsonPropertyName("F")] public int? FpsLimit { get; set; }
            [JsonPropertyName("G")] public bool? ClippingEnabled { get; set; }
            [JsonPropertyName("H")] public int? ClippingMinutes { get; set; }
            [JsonPropertyName("I")] public int? ClippingModifiers { get; set; }
            [JsonPropertyName("J")] public int? ClippingKey { get; set; }
            [JsonPropertyName("K")] public bool? SpeakerEnabled { get; set; }
            [JsonPropertyName("L")] public int? SpeakerVolume { get; set; }
            [JsonPropertyName("M")] public bool? MicrophoneEnabled { get; set; }
            [JsonPropertyName("N")] public int? MicrophoneVolume { get; set; }
            [JsonPropertyName("O")] public bool? AutoClickerEnabled { get; set; }
            [JsonPropertyName("P")] public int? AutoClickerModifiers { get; set; }
            [JsonPropertyName("Q")] public int? AutoClickerKey { get; set; }
            [JsonPropertyName("R")] public int? AutoClickerRate { get; set; }
            [JsonPropertyName("S")] public bool? HoldToSpamEnabled { get; set; }
            [JsonPropertyName("T")] public int? HoldToSpamRate { get; set; }
            [JsonPropertyName("U")] public bool? HandgunOnly { get; set; }
            [JsonPropertyName("V")] public List<string>? MissingWeapons { get; set; }
            [JsonPropertyName("W")] public string? PrimaryWeapon { get; set; }
            [JsonPropertyName("X")] public string? SecondaryWeapon { get; set; }
            [JsonPropertyName("Y")] public string? MeleeWeapon { get; set; }
            [JsonPropertyName("Z")] public string? UtilityWeapon { get; set; }
            [JsonPropertyName("0")] public bool? AutoRejoin { get; set; }
            [JsonPropertyName("1")] public bool? CheckForUpdates { get; set; }

            public static SharedSettings From(Settings source)
            {
                Settings defaults = new();
                bool selectedUserSky = source.CustomSkyboxEnabled &&
                    UserSkyboxService.IsUserSkyboxKey(source.CustomSkyboxSourceName);
                bool portableFont = IsPortableFontName(source.SelectedFontName);

                return new SharedSettings
                {
                    BootstrapperStyle = Different((int)source.BootstrapperStyle, (int)defaults.BootstrapperStyle),
                    BootstrapperIcon = source.BootstrapperIcon == Enums.BootstrapperIcon.IconCustom
                        ? null
                        : Different((int)source.BootstrapperIcon, (int)defaults.BootstrapperIcon),
                    BootstrapperTitle = Different(source.BootstrapperTitle, defaults.BootstrapperTitle),
                    Theme = Different((int)source.Theme, (int)defaults.Theme),
                    MultiInstance = Different(source.MultiInstanceLaunching, defaults.MultiInstanceLaunching),
                    ConfirmLaunches = Different(source.ConfirmLaunches, defaults.ConfirmLaunches),
                    Locale = Different(source.Locale, defaults.Locale),
                    ForceRobloxLanguage = Different(source.ForceRobloxLanguage, defaults.ForceRobloxLanguage),
                    UseFastFlagManager = Different(source.UseFastFlagManager, defaults.UseFastFlagManager),
                    SoftwareRender = Different(source.WPFSoftwareRender, defaults.WPFSoftwareRender),
                    Analytics = Different(source.EnableAnalytics, defaults.EnableAnalytics),
                    UpdateRoblox = Different(source.UpdateRoblox, defaults.UpdateRoblox),
                    BackgroundUpdates = Different(source.BackgroundUpdatesEnabled, defaults.BackgroundUpdatesEnabled),
                    CloseOnLaunch = Different(source.CloseSleepStrapOnLaunch, defaults.CloseSleepStrapOnLaunch),
                    OverrideLegacySettings = Different(source.OverrideLegacyBloxstrapSettings, defaults.OverrideLegacyBloxstrapSettings),
                    Cleaner = Different((int)source.CleanerOptions, (int)defaults.CleanerOptions),
                    ActivityTracking = Different(source.EnableActivityTracking, defaults.EnableActivityTracking),
                    DiscordRichPresence = Different(source.UseDiscordRichPresence, defaults.UseDiscordRichPresence),
                    HideRpcButtons = Different(source.HideRPCButtons, defaults.HideRPCButtons),
                    ShowAccount = Different(source.ShowAccountOnRichPresence, defaults.ShowAccountOnRichPresence),
                    ShowServerDetails = Different(source.ShowServerDetails, defaults.ShowServerDetails),
                    ShowServerUptime = Different(source.ShowServerUptime, defaults.ShowServerUptime),
                    DisableAppPatch = Different(source.UseDisableAppPatch, defaults.UseDisableAppPatch),
                    DarkTextures = Different(source.DarkTexturesEnabled, defaults.DarkTexturesEnabled),
                    RtxShine = Different(source.RtxShineEnabled, defaults.RtxShineEnabled),
                    // An empty string is the compact, explicit marker for the
                    // Roblox default. Null means the sender used a local custom
                    // font that cannot be transferred, so leave the receiver's
                    // existing font untouched.
                    FontName = portableFont
                        ? String.Equals(source.SelectedFontName, "Roblox Default", StringComparison.OrdinalIgnoreCase)
                            ? String.Empty
                            : source.SelectedFontName
                        : null,
                    FontColor = Different(source.SelectedFontColor, defaults.SelectedFontColor),
                    BuiltInSky = selectedUserSky
                        ? null
                        : source.CustomSkyboxEnabled && SkyboxGalleryService.IsPreset(source.CustomSkyboxSourceName)
                            ? source.CustomSkyboxSourceName
                            : String.Empty,
                    FavoriteSkyboxes = (source.FavoriteSkyboxes ?? new List<string>())
                        .Where(SkyboxGalleryService.IsPreset)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() is { Count: > 0 } favorites ? favorites : null,
                    StretchPercent = Different(source.RivalsStretchPercent, defaults.RivalsStretchPercent),
                    FpsLimit = Different(source.RivalsFpsLimit, defaults.RivalsFpsLimit),
                    ClippingEnabled = Different(source.ClippingEnabled, defaults.ClippingEnabled),
                    ClippingMinutes = Different(source.ClippingBufferMinutes, defaults.ClippingBufferMinutes),
                    ClippingModifiers = Different(source.ClippingHotkeyModifiers, defaults.ClippingHotkeyModifiers),
                    ClippingKey = Different(source.ClippingHotkeyVirtualKey, defaults.ClippingHotkeyVirtualKey),
                    SpeakerEnabled = Different(source.ClippingSpeakerEnabled, defaults.ClippingSpeakerEnabled),
                    SpeakerVolume = Different(source.ClippingSpeakerVolume, defaults.ClippingSpeakerVolume),
                    MicrophoneEnabled = Different(source.ClippingMicrophoneEnabled, defaults.ClippingMicrophoneEnabled),
                    MicrophoneVolume = Different(source.ClippingMicrophoneVolume, defaults.ClippingMicrophoneVolume),
                    AutoClickerEnabled = Different(source.ExperimentalAutoClickerEnabled, defaults.ExperimentalAutoClickerEnabled),
                    AutoClickerModifiers = Different(source.ExperimentalAutoClickerHotkeyModifiers, defaults.ExperimentalAutoClickerHotkeyModifiers),
                    AutoClickerKey = Different(source.ExperimentalAutoClickerHotkeyVirtualKey, defaults.ExperimentalAutoClickerHotkeyVirtualKey),
                    AutoClickerRate = Different(source.ExperimentalAutoClickerClicksPerSecond, defaults.ExperimentalAutoClickerClicksPerSecond),
                    HoldToSpamEnabled = Different(source.ExperimentalRobloxHoldToSpamEnabled, defaults.ExperimentalRobloxHoldToSpamEnabled),
                    HoldToSpamRate = Different(source.ExperimentalRobloxHoldToSpamClicksPerSecond, defaults.ExperimentalRobloxHoldToSpamClicksPerSecond),
                    HandgunOnly = Different(source.ExperimentalRobloxHoldToSpamHandgunOnly, defaults.ExperimentalRobloxHoldToSpamHandgunOnly),
                    MissingWeapons = source.MacroMissingWeapons is { Count: > 0 } missingWeapons ? missingWeapons.ToList() : null,
                    PrimaryWeapon = Different(source.MacroPrimaryWeapon, defaults.MacroPrimaryWeapon),
                    SecondaryWeapon = Different(source.MacroSecondaryWeapon, defaults.MacroSecondaryWeapon),
                    MeleeWeapon = Different(source.MacroMeleeWeapon, defaults.MacroMeleeWeapon),
                    UtilityWeapon = Different(source.MacroUtilityWeapon, defaults.MacroUtilityWeapon),
                    AutoRejoin = Different(source.MacroAutoRejoinHourly, defaults.MacroAutoRejoinHourly),
                    CheckForUpdates = Different(source.CheckForUpdates, defaults.CheckForUpdates)
                };
            }

            public void Validate()
            {
                ValidateEnum<Enums.BootstrapperStyle>(BootstrapperStyle);
                if (BootstrapperIcon is int icon &&
                    (!Enum.IsDefined(typeof(Enums.BootstrapperIcon), icon) || (Enums.BootstrapperIcon)icon == Enums.BootstrapperIcon.IconCustom))
                {
                    throw new InvalidDataException("The settings code contains an unsupported launcher icon.");
                }
                ValidateEnum<Enums.Theme>(Theme);
                ValidateEnum<Enums.CleanerOptions>(Cleaner);
                ValidateLength(BootstrapperTitle, 128, "title");
                ValidateLocale(Locale);
                ValidateLength(FontName, 128, "font");
                if (FontName is not null && FontName.Length > 0 && !IsPortableFontName(FontName))
                    throw new InvalidDataException("The settings code contains a non-portable font.");
                if (FontColor is not null && !Regex.IsMatch(FontColor, "^#[0-9A-Fa-f]{6}$"))
                    throw new InvalidDataException("The settings code contains an invalid text color.");
                if (BuiltInSky is not null && BuiltInSky.Length > 0 && !SkyboxGalleryService.IsPreset(BuiltInSky))
                    throw new InvalidDataException("The settings code contains an unknown skybox.");
                ValidateSkyboxNames(FavoriteSkyboxes);
                ValidateRange(StretchPercent, 50, 100, "stretch");
                if (FpsLimit is int fps && !ValidFpsLimits.Contains(fps))
                    throw new InvalidDataException("The settings code contains an unsupported FPS limit.");
                ValidateRange(ClippingMinutes, 1, 10, "replay length");
                ValidateModifier(ClippingModifiers, "clip hotkey");
                ValidateVirtualKey(ClippingKey, "clip hotkey");
                ValidateRange(SpeakerVolume, 0, 100, "speaker volume");
                ValidateRange(MicrophoneVolume, 0, 100, "microphone volume");
                ValidateModifier(AutoClickerModifiers, "auto clicker hotkey");
                ValidateVirtualKey(AutoClickerKey, "auto clicker hotkey");
                ValidateRange(AutoClickerRate, 1, 50, "auto clicker speed");
                ValidateRange(HoldToSpamRate, 1, 50, "hold-to-spam speed");
                ValidateWeaponList(MissingWeapons);
                ValidateWeaponName(PrimaryWeapon);
                ValidateWeaponName(SecondaryWeapon);
                ValidateWeaponName(MeleeWeapon);
                ValidateWeaponName(UtilityWeapon);
            }

            public void ApplyTo(Settings target)
            {
                Settings defaults = new();
                target.BootstrapperStyle = (Enums.BootstrapperStyle)Get(BootstrapperStyle, (int)defaults.BootstrapperStyle);
                if (BootstrapperIcon is int icon)
                    target.BootstrapperIcon = (Enums.BootstrapperIcon)icon;
                target.BootstrapperTitle = Get(BootstrapperTitle, defaults.BootstrapperTitle);
                target.Theme = (Enums.Theme)Get(Theme, (int)defaults.Theme);
                target.MultiInstanceLaunching = Get(MultiInstance, defaults.MultiInstanceLaunching);
                target.ConfirmLaunches = Get(ConfirmLaunches, defaults.ConfirmLaunches);
                target.Locale = Get(Locale, defaults.Locale);
                target.ForceRobloxLanguage = Get(ForceRobloxLanguage, defaults.ForceRobloxLanguage);
                target.UseFastFlagManager = Get(UseFastFlagManager, defaults.UseFastFlagManager);
                target.WPFSoftwareRender = Get(SoftwareRender, defaults.WPFSoftwareRender);
                target.EnableAnalytics = Get(Analytics, defaults.EnableAnalytics);
                target.UpdateRoblox = Get(UpdateRoblox, defaults.UpdateRoblox);
                target.BackgroundUpdatesEnabled = Get(BackgroundUpdates, defaults.BackgroundUpdatesEnabled);
                target.CloseSleepStrapOnLaunch = Get(CloseOnLaunch, defaults.CloseSleepStrapOnLaunch);
                target.OverrideLegacyBloxstrapSettings = Get(OverrideLegacySettings, defaults.OverrideLegacyBloxstrapSettings);
                target.CleanerOptions = (Enums.CleanerOptions)Get(Cleaner, (int)defaults.CleanerOptions);
                target.EnableActivityTracking = Get(ActivityTracking, defaults.EnableActivityTracking);
                target.UseDiscordRichPresence = Get(DiscordRichPresence, defaults.UseDiscordRichPresence);
                target.HideRPCButtons = Get(HideRpcButtons, defaults.HideRPCButtons);
                target.ShowAccountOnRichPresence = Get(ShowAccount, defaults.ShowAccountOnRichPresence);
                target.ShowServerDetails = Get(ShowServerDetails, defaults.ShowServerDetails);
                target.ShowServerUptime = Get(ShowServerUptime, defaults.ShowServerUptime);
                target.UseDisableAppPatch = Get(DisableAppPatch, defaults.UseDisableAppPatch);
                target.DarkTexturesEnabled = Get(DarkTextures, defaults.DarkTexturesEnabled);
                target.RtxShineEnabled = Get(RtxShine, defaults.RtxShineEnabled);
                if (FontName is not null)
                {
                    target.SelectedFontName = FontName;
                    target.SelectedFontSource = String.Empty;
                }
                target.SelectedFontColor = Get(FontColor, defaults.SelectedFontColor).ToUpperInvariant();

                if (BuiltInSky is not null)
                {
                    target.CustomSkyboxEnabled = !String.IsNullOrEmpty(BuiltInSky);
                    target.CustomSkyboxSourceName = BuiltInSky;
                }
                target.FavoriteSkyboxes = FavoriteSkyboxes?.ToList() ?? new List<string>();

                // Do not import the active stretch state: it contains a monitor's
                // native mode and changing it can affect an entire desktop. The
                // preference is preserved, but the receiver can safely enable it.
                target.RivalsStretchPercent = Get(StretchPercent, defaults.RivalsStretchPercent);
                ApplyFpsLimit(target, Get(FpsLimit, defaults.RivalsFpsLimit));

                target.ClippingEnabled = Get(ClippingEnabled, defaults.ClippingEnabled);
                target.ClippingBufferMinutes = Get(ClippingMinutes, defaults.ClippingBufferMinutes);
                target.ClippingHotkeyModifiers = Get(ClippingModifiers, defaults.ClippingHotkeyModifiers);
                target.ClippingHotkeyVirtualKey = Get(ClippingKey, defaults.ClippingHotkeyVirtualKey);
                target.ClippingSpeakerEnabled = Get(SpeakerEnabled, defaults.ClippingSpeakerEnabled);
                target.ClippingSpeakerVolume = Get(SpeakerVolume, defaults.ClippingSpeakerVolume);
                target.ClippingMicrophoneEnabled = Get(MicrophoneEnabled, defaults.ClippingMicrophoneEnabled);
                target.ClippingMicrophoneVolume = Get(MicrophoneVolume, defaults.ClippingMicrophoneVolume);

                target.ExperimentalAutoClickerEnabled = Get(AutoClickerEnabled, defaults.ExperimentalAutoClickerEnabled);
                target.ExperimentalAutoClickerHotkeyModifiers = Get(AutoClickerModifiers, defaults.ExperimentalAutoClickerHotkeyModifiers);
                target.ExperimentalAutoClickerHotkeyVirtualKey = Get(AutoClickerKey, defaults.ExperimentalAutoClickerHotkeyVirtualKey);
                target.ExperimentalAutoClickerClicksPerSecond = Get(AutoClickerRate, defaults.ExperimentalAutoClickerClicksPerSecond);
                target.ExperimentalRobloxHoldToSpamEnabled = Get(HoldToSpamEnabled, defaults.ExperimentalRobloxHoldToSpamEnabled);
                target.ExperimentalRobloxHoldToSpamClicksPerSecond = Get(HoldToSpamRate, defaults.ExperimentalRobloxHoldToSpamClicksPerSecond);
                target.ExperimentalRobloxHoldToSpamHandgunOnly = Get(HandgunOnly, defaults.ExperimentalRobloxHoldToSpamHandgunOnly);

                target.MacroMissingWeapons = MissingWeapons?.ToList() ?? new List<string>();
                target.MacroPrimaryWeapon = Get(PrimaryWeapon, defaults.MacroPrimaryWeapon);
                target.MacroSecondaryWeapon = Get(SecondaryWeapon, defaults.MacroSecondaryWeapon);
                target.MacroMeleeWeapon = Get(MeleeWeapon, defaults.MacroMeleeWeapon);
                target.MacroUtilityWeapon = Get(UtilityWeapon, defaults.MacroUtilityWeapon);
                target.MacroAutoRejoinHourly = Get(AutoRejoin, defaults.MacroAutoRejoinHourly);
                target.CheckForUpdates = Get(CheckForUpdates, defaults.CheckForUpdates);
            }

            private static int? Different(int value, int defaultValue) => value == defaultValue ? null : value;
            private static bool? Different(bool value, bool defaultValue) => value == defaultValue ? null : value;
            private static string? Different(string? value, string? defaultValue) =>
                String.Equals(value, defaultValue, StringComparison.Ordinal) ? null : value ?? String.Empty;
            private static int Get(int? value, int defaultValue) => value ?? defaultValue;
            private static bool Get(bool? value, bool defaultValue) => value ?? defaultValue;
            private static string Get(string? value, string defaultValue) => value ?? defaultValue;

            private static bool IsPortableFontName(string? name) =>
                String.Equals(name, "Roblox Default", StringComparison.OrdinalIgnoreCase) ||
                name?.StartsWith("Montserrat", StringComparison.OrdinalIgnoreCase) == true ||
                name?.StartsWith("Roblox Â· ", StringComparison.OrdinalIgnoreCase) == true;

            private static void ValidateEnum<TEnum>(int? value) where TEnum : struct, Enum
            {
                if (value is int raw && !Enum.IsDefined(typeof(TEnum), raw))
                    throw new InvalidDataException("The settings code contains an unsupported setting value.");
            }

            private static void ValidateRange(int? value, int minimum, int maximum, string name)
            {
                if (value is int number && (number < minimum || number > maximum))
                    throw new InvalidDataException($"The settings code contains an invalid {name}.");
            }

            private static void ValidateModifier(int? value, string name)
            {
                if (value is int modifier && (modifier < 0 || modifier > 15))
                    throw new InvalidDataException($"The settings code contains an invalid {name}.");
            }

            private static void ValidateVirtualKey(int? value, string name)
            {
                if (value is int key && (key < 1 || key > 254))
                    throw new InvalidDataException($"The settings code contains an invalid {name}.");
            }

            private static void ValidateLength(string? value, int maximum, string name)
            {
                if (value is not null && value.Length > maximum)
                    throw new InvalidDataException($"The settings code contains an invalid {name}.");
            }

            private static void ValidateLocale(string? value)
            {
                if (value is not null && !Regex.IsMatch(value, "^[A-Za-z_-]{1,16}$"))
                    throw new InvalidDataException("The settings code contains an invalid language setting.");
            }

            private static void ValidateSkyboxNames(IEnumerable<string>? names)
            {
                if (names is null)
                    return;

                if (names.Count() > 32 || names.Any(name => !SkyboxGalleryService.IsPreset(name)))
                    throw new InvalidDataException("The settings code contains an invalid skybox favorite.");
            }

            private static void ValidateWeaponList(IEnumerable<string>? names)
            {
                if (names is null)
                    return;

                if (names.Count() > 64)
                    throw new InvalidDataException("The settings code contains too many calibration entries.");
                foreach (string name in names)
                    ValidateWeaponName(name);
            }

            private static void ValidateWeaponName(string? name)
            {
                if (name is not null && (name.Length > 64 || !Regex.IsMatch(name, "^[A-Za-z0-9 .'-]+$")))
                    throw new InvalidDataException("The settings code contains an invalid weapon name.");
            }

            private static void ApplyFpsLimit(Settings target, int newLimit)
            {
                const string targetFpsFlag = "DFIntTaskSchedulerTargetFps";
                const string unlockFpsFlag = "FFlagTaskSchedulerLimitTargetFpsTo2402";
                const string missingFlagValue = "__SLEEPSTRAP_FLAG_WAS_MISSING__";
                int oldLimit = target.RivalsFpsLimit;
                Dictionary<string, string> backup = target.RivalsFpsFlagBackup ??= new Dictionary<string, string>();

                if (oldLimit == newLimit)
                {
                    target.RivalsFpsLimit = newLimit;
                    return;
                }

                if (newLimit == 0)
                {
                    foreach (string key in new[] { targetFpsFlag, unlockFpsFlag })
                    {
                        if (!backup.TryGetValue(key, out string? previous))
                            continue;

                        string ownedValue = key == targetFpsFlag ? oldLimit.ToString() : "False";
                        if (String.Equals(App.FastFlags.GetValue(key), ownedValue, StringComparison.Ordinal))
                            App.FastFlags.SetValue(key, previous == missingFlagValue ? null : previous);
                    }
                    backup.Clear();
                }
                else
                {
                    if (!backup.ContainsKey(targetFpsFlag))
                        backup[targetFpsFlag] = App.FastFlags.GetValue(targetFpsFlag) ?? missingFlagValue;
                    if (!backup.ContainsKey(unlockFpsFlag))
                        backup[unlockFpsFlag] = App.FastFlags.GetValue(unlockFpsFlag) ?? missingFlagValue;

                    App.FastFlags.SetValue(targetFpsFlag, newLimit.ToString());
                    App.FastFlags.SetValue(unlockFpsFlag, "False");
                    target.UseFastFlagManager = true;
                }

                target.RivalsFpsLimit = newLimit;
                App.FastFlags.Save();
            }
        }
    }
}
