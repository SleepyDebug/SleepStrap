namespace SleepStrap.Services
{
    internal static class RivalsFpsCounterService
    {
        private const string MissingFlagValue = "__SLEEPSTRAP_FLAG_WAS_MISSING__";

        // Keep the legacy gates as compatibility helpers, but the saved Roblox
        // PerformanceStatsVisible preference is what makes the current UI appear.
        internal static IReadOnlyDictionary<string, string> ManagedFlags { get; } = new Dictionary<string, string>
        {
            ["FFlagDebugDisplayFPS"] = "True",
            ["FFlagDebugAlwaysDisplayRenderStats"] = "True"
        };

        public static void SetEnabled(bool enabled)
        {
            if (enabled)
                Enable();
            else
                Disable();

            SetPerformanceStatsVisible(enabled);
            App.Settings.Prop.UseFastFlagManager = true;
            App.FastFlags.Save();
        }

        public static void RefreshState()
        {
            if (App.Settings.Prop.RivalsFpsCounterEnabled)
                Enable();
            else if (App.Settings.Prop.RivalsFpsCounterFlagBackup.Count > 0)
                Disable();

            SetPerformanceStatsVisible(App.Settings.Prop.RivalsFpsCounterEnabled);
            App.Settings.Prop.UseFastFlagManager = true;
            App.FastFlags.Save();
            App.Settings.Save();
        }

        private static void Enable()
        {
            Dictionary<string, string> backup = App.Settings.Prop.RivalsFpsCounterFlagBackup;
            foreach ((string key, string value) in ManagedFlags)
            {
                if (!backup.ContainsKey(key))
                    backup[key] = App.FastFlags.GetValue(key) ?? MissingFlagValue;

                App.FastFlags.SetValue(key, value);
            }
        }

        private static void Disable()
        {
            Dictionary<string, string> backup = App.Settings.Prop.RivalsFpsCounterFlagBackup;
            foreach ((string key, string ownedValue) in ManagedFlags)
            {
                if (!backup.TryGetValue(key, out string? previous))
                    continue;

                if (String.Equals(App.FastFlags.GetValue(key), ownedValue, StringComparison.Ordinal))
                    App.FastFlags.SetValue(key, previous == MissingFlagValue ? null : previous);

                backup.Remove(key);
            }

            backup.Clear();
        }

        private static void SetPerformanceStatsVisible(bool visible)
        {
            // Roblox can rewrite this file when it exits, so reload it immediately
            // before every launch and then save the requested state.
            App.GlobalSettings.Load();
            if (!App.GlobalSettings.Loaded)
                return;

            App.GlobalSettings.SetPreset(
                "User.PerformanceStatsVisible",
                visible ? "true" : "false");
            App.GlobalSettings.Save();
        }
    }
}
