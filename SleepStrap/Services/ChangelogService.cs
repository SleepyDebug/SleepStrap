using System.Windows;
using SleepStrap.UI.Elements.Dialogs;

namespace SleepStrap.Services
{
    internal static class ChangelogService
    {
        public static void ShowIfNewVersion(Window owner)
        {
            string version = new Version(App.Version).ToString(3);
            if (String.Equals(App.Settings.Prop.LastShownChangelogVersion, version, StringComparison.OrdinalIgnoreCase))
                return;

            var changelog = new ChangelogWindow(version) { Owner = owner };
            changelog.ShowDialog();
            App.Settings.Prop.LastShownChangelogVersion = version;
            App.Settings.Save();
        }
    }
}
