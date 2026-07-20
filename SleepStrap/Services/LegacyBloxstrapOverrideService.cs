namespace SleepStrap.Services
{
    internal static class LegacyBloxstrapOverrideService
    {
        private const string MissingValue = "__SLEEPSTRAP_FLAG_WAS_MISSING__";

        private static readonly IReadOnlyDictionary<string, string> Overrides = new Dictionary<string, string>
        {
            // A common Bloxstrap visual preset. Explicitly setting it false prevents
            // an old copied ClientAppSettings file from forcing every sky grey.
            ["FFlagDebugSkyGray"] = "False"
        };

        public static void SetEnabled(bool enabled)
        {
            Dictionary<string, string> backup = App.Settings.Prop.LegacyBloxstrapFlagBackup;

            if (enabled)
            {
                foreach ((string key, string value) in Overrides)
                {
                    if (!backup.ContainsKey(key))
                        backup[key] = App.FastFlags.GetValue(key) ?? MissingValue;
                    App.FastFlags.SetValue(key, value);
                }

                App.Settings.Prop.UseFastFlagManager = true;
            }
            else
            {
                foreach ((string key, string ownedValue) in Overrides)
                {
                    if (!backup.TryGetValue(key, out string? previous))
                        continue;
                    if (String.Equals(App.FastFlags.GetValue(key), ownedValue, StringComparison.Ordinal))
                        App.FastFlags.SetValue(key, previous == MissingValue ? null : previous);
                }

                backup.Clear();
            }

            App.FastFlags.Save();
        }

        public static void PrepareForLaunch()
        {
            if (!App.Settings.Prop.OverrideLegacyBloxstrapSettings)
                return;

            SetEnabled(true);
            App.Settings.Save();
        }
    }
}
