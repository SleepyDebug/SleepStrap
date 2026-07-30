using System.Windows;

namespace SleepStrap.Services
{
    internal static class ChangelogService
    {
        public static void ShowIfNewVersion()
        {
            string version = new Version(App.Version).ToString(3);
            if (String.Equals(App.Settings.Prop.LastShownChangelogVersion, version, StringComparison.OrdinalIgnoreCase))
                return;

            const string changes = "- Added Favoriting\n- Text Colors (beta)\n- Gradient Background\n- Blur preview inside SleepStrap\n- Cool stuff\n\nMade by sleepy :)\n\nClick OK, whatever, to continue to SleepStrap.";
            Frontend.ShowMessageBox($"SleepStrap {version}\n\n{changes}", MessageBoxImage.Information, MessageBoxButton.OK);
            App.Settings.Prop.LastShownChangelogVersion = version;
            App.Settings.Save();
        }
    }
}
