using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class OtherViewModel : NotifyPropertyChangedViewModel
    {
        public OtherViewModel()
        {
            ResetSettingsCommand = new RelayCommand(ResetSettings);
        }

        public string VersionText => $"SleepStrap {new Version(App.Version).ToString(3)}";
        public ICommand ResetSettingsCommand { get; }

        private void ResetSettings()
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                "Reset all SleepStrap settings to their defaults? This will remove your selected textures, skybox, font, clipping and PC preferences.",
                MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                App.Settings.Prop = new Models.Persistable.Settings();
                App.Settings.Save();
                Frontend.ShowMessageBox("SleepStrap settings were reset. Reopen SleepStrap to reload the defaults.", MessageBoxImage.Information);
                OnPropertyChanged(nameof(CloseSleepStrapOnLaunch));
                OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("OtherViewModel::ResetSettings", ex);
                Frontend.ShowMessageBox($"SleepStrap could not reset its settings.\n\n{ex.Message}", MessageBoxImage.Error);
            }
        }

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
