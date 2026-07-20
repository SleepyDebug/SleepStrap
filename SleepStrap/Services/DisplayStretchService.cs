using System.Runtime.InteropServices;

namespace SleepStrap.Services
{
    internal static class DisplayStretchService
    {
        private const int EnumCurrentSettings = -1;
        private const int DispChangeSuccessful = 0;
        private const int DmPelsWidth = 0x00080000;
        private const int DmPelsHeight = 0x00100000;
        private const int DmDisplayFrequency = 0x00400000;

        internal readonly record struct DisplayMode(int Width, int Height, int Frequency)
        {
            public override string ToString() => $"{Width} × {Height}" + (Frequency > 0 ? $" @ {Frequency} Hz" : "");
        }

        public static DisplayMode GetCurrentMode()
        {
            DEVMODE mode = CreateMode();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
                throw new InvalidOperationException("Windows could not read the current display resolution.");

            return ToDisplayMode(mode);
        }

        public static DisplayMode ApplyHorizontalStretch(int percent, DisplayMode nativeMode)
        {
            percent = Math.Clamp(percent, 55, 95);
            int targetWidth = (int)Math.Round(nativeMode.Width * percent / 100d / 2d) * 2;

            List<DisplayMode> allNarrowerModes = GetSupportedModes()
                .Where(x => x.Width < nativeMode.Width && x.Width >= 800 && x.Height >= 600)
                .ToList();

            List<DisplayMode> supportedModes = allNarrowerModes
                .Where(x => x.Height == nativeMode.Height && x.Width < nativeMode.Width && x.Width >= 800)
                .OrderBy(x => Math.Abs(x.Width - targetWidth))
                .ThenBy(x => x.Frequency == nativeMode.Frequency ? 0 : 1)
                .ToList();

            if (supportedModes.Count > 0)
            {
                DisplayMode selected = supportedModes[0];
                ApplyMode(selected);
                return selected;
            }

            // Some drivers accept a GPU-scaled mode even when Windows does not list it.
            // Trying it directly makes stretching work on many laptops and secondary displays.
            var generatedMode = new DisplayMode(targetWidth, nativeMode.Height, nativeMode.Frequency);
            if (TryApplyMode(generatedMode, out _))
                return generatedMode;

            double targetAspect = targetWidth / (double)nativeMode.Height;
            double nativeAspect = nativeMode.Width / (double)nativeMode.Height;
            DisplayMode fallback = allNarrowerModes
                .Where(x => x.Width / (double)x.Height < nativeAspect - 0.03)
                .OrderBy(x => Math.Abs((x.Width / (double)x.Height) - targetAspect))
                .ThenBy(x => Math.Abs(x.Height - nativeMode.Height))
                .ThenBy(x => x.Frequency == nativeMode.Frequency ? 0 : 1)
                .FirstOrDefault();

            if (fallback.Width > 0 && TryApplyMode(fallback, out _))
                return fallback;

            throw new InvalidOperationException(
                $"This display driver does not expose a stretchable resolution near {targetWidth} x {nativeMode.Height}. " +
                "Add that custom resolution in your NVIDIA, AMD, or Intel graphics settings, then try again.");
        }

        public static void Restore(DisplayMode nativeMode)
        {
            if (nativeMode.Width <= 0 || nativeMode.Height <= 0)
            {
                int resetResult = ChangeDisplaySettings(IntPtr.Zero, 0);
                if (resetResult != DispChangeSuccessful)
                    throw new InvalidOperationException(GetDisplayError(resetResult));
                return;
            }

            ApplyMode(nativeMode);
        }

        private static IReadOnlyCollection<DisplayMode> GetSupportedModes()
        {
            HashSet<DisplayMode> modes = new();

            for (int index = 0; ; index++)
            {
                DEVMODE mode = CreateMode();
                if (!EnumDisplaySettings(null, index, ref mode))
                    break;

                modes.Add(ToDisplayMode(mode));
            }

            return modes;
        }

        private static void ApplyMode(DisplayMode displayMode)
        {
            if (!TryApplyMode(displayMode, out int result))
                throw new InvalidOperationException(GetDisplayError(result));
        }

        private static bool TryApplyMode(DisplayMode displayMode, out int result)
        {
            DEVMODE mode = CreateMode();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
            {
                result = -1;
                return false;
            }

            mode.dmPelsWidth = displayMode.Width;
            mode.dmPelsHeight = displayMode.Height;
            mode.dmDisplayFrequency = displayMode.Frequency;
            mode.dmFields = DmPelsWidth | DmPelsHeight;

            if (displayMode.Frequency > 0)
                mode.dmFields |= DmDisplayFrequency;

            result = ChangeDisplaySettings(ref mode, 0);
            return result == DispChangeSuccessful;
        }

        private static DEVMODE CreateMode() => new()
        {
            dmSize = (short)Marshal.SizeOf<DEVMODE>()
        };

        private static DisplayMode ToDisplayMode(DEVMODE mode) =>
            new(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmDisplayFrequency);

        private static string GetDisplayError(int result) => result switch
        {
            -1 => "Windows rejected the requested display mode.",
            -2 => "The display driver could not apply the requested mode.",
            -3 => "Windows could not save the display mode.",
            -4 => "The display mode is not supported by this graphics adapter.",
            -5 => "The display mode change requires elevated permission.",
            -6 => "The display mode failed because the current session is remote.",
            _ => $"Windows could not change the display mode (error {result})."
        };

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNumber, ref DEVMODE mode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int ChangeDisplaySettings(ref DEVMODE mode, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int ChangeDisplaySettings(IntPtr mode, uint flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }
    }
}
