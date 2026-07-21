using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class OtherViewModel : NotifyPropertyChangedViewModel
    {
        public string VersionText => $"SleepStrap {new Version(App.Version).ToString(3)}";

        public bool CloseSleepStrapOnLaunch
        {
            get => App.Settings.Prop.CloseSleepStrapOnLaunch;
            set
            {
                if (value == App.Settings.Prop.CloseSleepStrapOnLaunch)
                    return;

                App.Settings.Prop.CloseSleepStrapOnLaunch = value;
                App.Settings.Save();
                OnPropertyChanged(nameof(CloseSleepStrapOnLaunch));
            }
        }

        public bool OverrideLegacyBloxstrapSettings
        {
            get => App.Settings.Prop.OverrideLegacyBloxstrapSettings;
            set
            {
                if (value == App.Settings.Prop.OverrideLegacyBloxstrapSettings)
                    return;

                try
                {
                    LegacyBloxstrapOverrideService.SetEnabled(value);
                    App.Settings.Prop.OverrideLegacyBloxstrapSettings = value;
                    App.Settings.Save();
                    OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("OtherViewModel::OverrideLegacyBloxstrapSettings", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not update the FastFlag override.\n\n{ex.Message}", System.Windows.MessageBoxImage.Error);
                    OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings));
                }
            }
        }

    }
}
