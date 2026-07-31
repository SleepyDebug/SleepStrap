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
            RestoreNvidiaChangesCommand = new AsyncRelayCommand(RestoreNvidiaChangesAsync);
        }

        public string VersionText => $"SleepStrap {new Version(App.Version).ToString(3)}";
        public ICommand ResetSettingsCommand { get; }
        public IAsyncRelayCommand RestoreNvidiaChangesCommand { get; }

        private async Task RestoreNvidiaChangesAsync()
        {
            if (App.Settings.Prop.NvidiaBlurredTexturesProfileBackup.Count == 0)
            {
                Frontend.ShowMessageBox(
                    "SleepStrap does not have a saved NVIDIA profile backup to restore. Use NVIDIA Control Panel → Manage 3D settings → Program Settings → Roblox VR → Restore.",
                    MessageBoxImage.Information);
                return;
            }

            if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0)
            {
                Frontend.ShowMessageBox("Close Roblox before restoring the NVIDIA profile.", MessageBoxImage.Warning);
                return;
            }

            if (Frontend.ShowMessageBox(
                "Restore the saved NVIDIA Roblox VR profile now? This only restores values SleepStrap saved before Blur was enabled.",
                MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                NvidiaBlurElevationBridge.HelperResult result = await NvidiaBlurElevationBridge.RunElevatedAsync(
                    false,
                    App.Settings.Prop.NvidiaBlurredTexturesProfileBackup);
                if (!result.Success)
                    throw new InvalidOperationException(String.IsNullOrWhiteSpace(result.Error) ? "The NVIDIA profile could not be restored." : result.Error);

                App.Settings.Prop.NvidiaBlurredTexturesEnabled = false;
                App.Settings.Prop.NvidiaBlurredTexturesProfileBackup.Clear();
                App.Settings.Save();
                Frontend.ShowMessageBox("NVIDIA changes restored.", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("OtherViewModel::RestoreNvidiaChanges", ex);
                Frontend.ShowMessageBox($"SleepStrap could not restore the NVIDIA changes.\n\n{ex.Message}", MessageBoxImage.Error);
            }
        }

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
