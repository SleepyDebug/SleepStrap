namespace SleepStrap.Services
{
    internal static class BlurryTextureService
    {
        private const string MissingValue = "__SLEEPSTRAP_FLAG_WAS_MISSING__";

        // Force Roblox to sample lower mip levels while retaining its normal bilinear
        // filtering. This produces a visible low-resolution blur instead of merely lowering
        // the automatic quality preset. Flag ownership is isolated from sky/dark file mods.
        internal static IReadOnlyDictionary<string, string> ManagedFlags { get; } = new Dictionary<string, string>
        {
            ["DFFlagTextureQualityOverrideEnabled"] = "True",
            ["DFIntTextureQualityOverride"] = "0",
            ["DFIntDebugFRMQualityLevelOverride"] = "1",
            ["FIntDebugTextureManagerSkipMips"] = "4",
            ["FIntTextureCompositorLowResFactor"] = "4"
        };

        // Values written by earlier SleepStrap builds. They are restored only when the saved
        // backup proves SleepStrap owned the value and it has not since been changed manually.
        private static IReadOnlyDictionary<string, string> RetiredFlags { get; } = new Dictionary<string, string>
        {
            ["FIntDebugForceMSAASamples"] = "1",
            ["DFIntCSGLevelOfDetailSwitchingDistance"] = "250",
            ["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "500",
            ["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "750",
            ["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "1000",
            ["DFFlagSkipHighResolutiontextureMipsOnLowMemoryDevices2"] = "True",
            ["DFFlagUITexturePickMipLevels2"] = "True",
            ["FFlagUITextureUseDynamicRenderSettings"] = "True",
            ["FIntUITextureLowSingleTextureMemoryMB"] = "1",
            ["FIntUITextureMediumSingleTextureMemoryMB"] = "1",
            ["FIntTM2ViewportFrameMaxMips"] = "2"
        };

        public static void SetEnabled(bool enabled)
        {
            if (enabled)
                Enable();
            else
                Disable();

            App.Settings.Prop.UseFastFlagManager = true;
            App.FastFlags.Save();
        }

        public static void RefreshIfEnabled()
        {
            if (!App.Settings.Prop.BlurryTexturesEnabled)
                return;

            Enable();
            App.Settings.Prop.UseFastFlagManager = true;
            App.FastFlags.Save();
            App.Settings.Save();
        }

        private static void Enable()
        {
            Dictionary<string, string> backup = App.Settings.Prop.BlurryTexturesFlagBackup;

            RestoreOwnedFlags(RetiredFlags, backup);

            foreach ((string key, string value) in ManagedFlags)
            {
                if (!backup.ContainsKey(key))
                    backup[key] = App.FastFlags.GetValue(key) ?? MissingValue;

                App.FastFlags.SetValue(key, value);
            }
        }

        private static void Disable()
        {
            Dictionary<string, string> backup = App.Settings.Prop.BlurryTexturesFlagBackup;

            RestoreOwnedFlags(ManagedFlags, backup);
            RestoreOwnedFlags(RetiredFlags, backup);

            // Forget stale metadata without changing flags this version does not manage.
            backup.Clear();
        }

        private static void RestoreOwnedFlags(
            IReadOnlyDictionary<string, string> ownedValues,
            Dictionary<string, string> backup)
        {
            foreach ((string key, string ownedValue) in ownedValues)
            {
                if (!backup.TryGetValue(key, out string? previous))
                    continue;

                // Preserve a newer manual or preset change made after blur was enabled.
                if (String.Equals(App.FastFlags.GetValue(key), ownedValue, StringComparison.Ordinal))
                {
                    if (previous == MissingValue)
                        App.FastFlags.SetValue(key, null);
                    else
                        App.FastFlags.SetValue(key, previous);
                }

                backup.Remove(key);
            }
        }
    }
}
