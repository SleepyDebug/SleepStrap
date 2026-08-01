using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class OtherViewModel : NotifyPropertyChangedViewModel
    {
        private string _shareCode = String.Empty;
        private string _importCode = String.Empty;

        public OtherViewModel()
        {
            ResetSettingsCommand = new RelayCommand(ResetSettings);
            RestoreNvidiaChangesCommand = new AsyncRelayCommand(RestoreNvidiaChangesAsync);
            CopySettingsCommand = new RelayCommand(CopySettings);
            ImportSettingsCommand = new RelayCommand(ImportSettings, () => !String.IsNullOrWhiteSpace(ImportCode));
        }

        public string VersionText => $"{App.ProjectName} {new Version(App.Version).ToString(3)}";
        public ICommand ResetSettingsCommand { get; }
        public IAsyncRelayCommand RestoreNvidiaChangesCommand { get; }
        public IRelayCommand CopySettingsCommand { get; }
        public IRelayCommand ImportSettingsCommand { get; }
        public string ShareCode { get => _shareCode; private set { if (String.Equals(_shareCode, value, StringComparison.Ordinal)) return; _shareCode = value; OnPropertyChanged(nameof(ShareCode)); } }
        public string ImportCode { get => _importCode; set { string code = value ?? String.Empty; if (String.Equals(_importCode, code, StringComparison.Ordinal)) return; _importCode = code; OnPropertyChanged(nameof(ImportCode)); ImportSettingsCommand.NotifyCanExecuteChanged(); } }

        private async Task RestoreNvidiaChangesAsync()
        {
            if (App.Settings.Prop.NvidiaBlurredTexturesProfileBackup.Count == 0) { Frontend.ShowMessageBox($"{App.ProjectName} does not have a saved NVIDIA profile backup to restore. Use NVIDIA Control Panel → Manage 3D settings → Program Settings → Roblox VR → Restore.", MessageBoxImage.Information); return; }
            if (Process.GetProcessesByName("RobloxPlayerBeta").Length > 0) { Frontend.ShowMessageBox("Close Roblox before restoring the NVIDIA profile.", MessageBoxImage.Warning); return; }
            if (Frontend.ShowMessageBox($"Restore the saved NVIDIA Roblox VR profile now? This only restores values {App.ProjectName} saved before Blur was enabled.", MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                NvidiaBlurElevationBridge.HelperResult result = await NvidiaBlurElevationBridge.RunElevatedAsync(false, App.Settings.Prop.NvidiaBlurredTexturesProfileBackup);
                if (!result.Success) throw new InvalidOperationException(String.IsNullOrWhiteSpace(result.Error) ? "The NVIDIA profile could not be restored." : result.Error);
                App.Settings.Prop.NvidiaBlurredTexturesEnabled = false; App.Settings.Prop.NvidiaBlurredTexturesProfileBackup.Clear(); App.Settings.Save(); Frontend.ShowMessageBox("NVIDIA changes restored.", MessageBoxImage.Information);
            }
            catch (Exception ex) { App.Logger.WriteException("OtherViewModel::RestoreNvidiaChanges", ex); Frontend.ShowMessageBox($"{App.ProjectName} could not restore the NVIDIA changes.\n\n{ex.Message}", MessageBoxImage.Error); }
        }

        private void ResetSettings()
        {
            if (Frontend.ShowMessageBox($"Reset all {App.ProjectName} settings to their defaults? This permanently deletes all custom skyboxes and removes your selected textures, skybox, font, clipping and PC preferences.", MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                try { VisualModService.RemoveCustomSkybox(); } catch (Exception ex) { App.Logger.WriteException("OtherViewModel::ResetSettingsRestoreSky", ex); }
                UserSkyboxService.DeleteAll(); App.Settings.Prop = new Models.Persistable.Settings(); App.Settings.Save(); SkyboxGalleryService.NotifyGalleryChanged(); ChatShortcutsHostService.Stop();
                Frontend.ShowMessageBox($"{App.ProjectName} settings were reset. Reopen {App.ProjectName} to reload the defaults.", MessageBoxImage.Information);
                OnPropertyChanged(nameof(CloseSleepStrapOnLaunch)); OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings)); OnPropertyChanged(nameof(ChatShortcutsEnabled));
            }
            catch (Exception ex) { App.Logger.WriteException("OtherViewModel::ResetSettings", ex); Frontend.ShowMessageBox($"{App.ProjectName} could not reset its settings.\n\n{ex.Message}", MessageBoxImage.Error); }
        }

        private void CopySettings()
        {
            try { ShareCode = SettingsShareService.CreateCode(App.Settings.Prop); try { Clipboard.SetText(ShareCode); Frontend.ShowMessageBox("Your compact settings code was copied.", MessageBoxImage.Information); } catch (Exception ex) { App.Logger.WriteException("OtherViewModel::CopySettingsClipboard", ex); Frontend.ShowMessageBox("Your settings code is ready below. Windows could not open the clipboard, so copy the text manually.", MessageBoxImage.Information); } }
            catch (Exception ex) { App.Logger.WriteException("OtherViewModel::CopySettings", ex); Frontend.ShowMessageBox($"{App.ProjectName} could not create a settings code.\n\n{ex.Message}", MessageBoxImage.Error); }
        }
        private void ImportSettings()
        {
            if (String.IsNullOrWhiteSpace(ImportCode)) return;
            if (Frontend.ShowMessageBox("Import these portable SleepBlox settings? Your custom imported skyboxes, device choices, local backups, and file paths will stay on this PC.", MessageBoxImage.Question, MessageBoxButton.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                SettingsShareService.ApplyCode(ImportCode, App.Settings.Prop); SettingsShareService.TryApplyPortableFont(App.Settings.Prop); App.Settings.Save(); ExperimentalClickerHostService.Refresh(); AutoRejoinSchedulerService.Refresh(); ChatShortcutsHostService.Refresh(); SkyboxGalleryService.NotifyGalleryChanged();
                OnPropertyChanged(nameof(CloseSleepStrapOnLaunch)); OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings)); OnPropertyChanged(nameof(ChatShortcutsEnabled)); ImportCode = String.Empty;
                Frontend.ShowMessageBox("Settings imported. Texture, sky, and font changes apply when you next launch Roblox.", MessageBoxImage.Information);
            }
            catch (Exception ex) { App.Logger.WriteException("OtherViewModel::ImportSettings", ex); Frontend.ShowMessageBox($"{App.ProjectName} could not import that settings code.\n\n{ex.Message}", MessageBoxImage.Error); }
        }
        public bool CloseSleepStrapOnLaunch { get => App.Settings.Prop.CloseSleepStrapOnLaunch; set { if (value == App.Settings.Prop.CloseSleepStrapOnLaunch) return; App.Settings.Prop.CloseSleepStrapOnLaunch = value; App.Settings.Save(); OnPropertyChanged(nameof(CloseSleepStrapOnLaunch)); } }
        public bool ChatShortcutsEnabled { get => App.Settings.Prop.ChatShortcutsEnabled; set { if (value == App.Settings.Prop.ChatShortcutsEnabled) return; App.Settings.Prop.ChatShortcutsEnabled = value; App.Settings.Save(); ChatShortcutsHostService.Refresh(); OnPropertyChanged(nameof(ChatShortcutsEnabled)); } }
        public bool OverrideLegacyBloxstrapSettings
        {
            get => App.Settings.Prop.OverrideLegacyBloxstrapSettings;
            set
            {
                if (value == App.Settings.Prop.OverrideLegacyBloxstrapSettings) return;
                try { LegacyBloxstrapOverrideService.SetEnabled(value); App.Settings.Prop.OverrideLegacyBloxstrapSettings = value; App.Settings.Save(); OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings)); }
                catch (Exception ex) { App.Logger.WriteException("OtherViewModel::OverrideLegacyBloxstrapSettings", ex); Frontend.ShowMessageBox($"{App.ProjectName} could not update the FastFlag override.\n\n{ex.Message}", MessageBoxImage.Error); OnPropertyChanged(nameof(OverrideLegacyBloxstrapSettings)); }
            }
        }
    }
}
