using System.IO;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public sealed class ExperimentalViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isImportingCustomSkybox;
        private string _customSkyboxName = "";
        private string _customSkyboxSourcePath = "";
        private IReadOnlyList<string> _customSkyboxTexturePaths = Array.Empty<string>();
        private string _customSkyboxStatus = "Choose a 2:1 panorama or 4x3 cube-cross PNG and give it a name.";

        public ExperimentalViewModel()
        {
            ChooseCustomSkyboxPngCommand = new RelayCommand(ChooseCustomSkyboxPng, () => !IsImportingCustomSkybox);
            ImportCustomSkyboxCommand = new AsyncRelayCommand(
                ImportCustomSkyboxAsync,
                () => !IsImportingCustomSkybox &&
                      !String.IsNullOrWhiteSpace(CustomSkyboxName) &&
                      File.Exists(CustomSkyboxSourcePath));
            ChooseCustomSkyboxTexturesCommand = new RelayCommand(ChooseCustomSkyboxTextures, () => !IsImportingCustomSkybox);
            ImportCustomSkyboxTexturesCommand = new AsyncRelayCommand(
                ImportCustomSkyboxTexturesAsync,
                () => !IsImportingCustomSkybox &&
                      !String.IsNullOrWhiteSpace(CustomSkyboxName) &&
                      _customSkyboxTexturePaths.Count == 6);
        }

        public IRelayCommand ChooseCustomSkyboxPngCommand { get; }
        public IAsyncRelayCommand ImportCustomSkyboxCommand { get; }
        public IRelayCommand ChooseCustomSkyboxTexturesCommand { get; }
        public IAsyncRelayCommand ImportCustomSkyboxTexturesCommand { get; }

        public bool AutoClickerEnabled
        {
            get => App.Settings.Prop.ExperimentalAutoClickerEnabled;
            set
            {
                if (value == App.Settings.Prop.ExperimentalAutoClickerEnabled)
                    return;

                App.Settings.Prop.ExperimentalAutoClickerEnabled = value;
                SaveAndRefresh(nameof(AutoClickerEnabled));
                OnPropertyChanged(nameof(AutoClickerStatus));
            }
        }

        public int ClicksPerSecond
        {
            get => Math.Clamp(App.Settings.Prop.ExperimentalAutoClickerClicksPerSecond, 1, 50);
            set
            {
                int clicksPerSecond = Math.Clamp(value, 1, 50);
                if (clicksPerSecond == App.Settings.Prop.ExperimentalAutoClickerClicksPerSecond)
                    return;

                App.Settings.Prop.ExperimentalAutoClickerClicksPerSecond = clicksPerSecond;
                SaveAndRefresh(nameof(ClicksPerSecond));
            }
        }

        public bool RobloxHoldToSpamEnabled
        {
            get => App.Settings.Prop.ExperimentalRobloxHoldToSpamEnabled;
            set
            {
                if (value == App.Settings.Prop.ExperimentalRobloxHoldToSpamEnabled)
                    return;

                App.Settings.Prop.ExperimentalRobloxHoldToSpamEnabled = value;
                SaveAndRefresh(nameof(RobloxHoldToSpamEnabled));
            }
        }

        public int RobloxHoldToSpamClicksPerSecond
        {
            get => Math.Clamp(App.Settings.Prop.ExperimentalRobloxHoldToSpamClicksPerSecond, 1, 50);
            set
            {
                int clicksPerSecond = Math.Clamp(value, 1, 50);
                if (clicksPerSecond == App.Settings.Prop.ExperimentalRobloxHoldToSpamClicksPerSecond)
                    return;

                App.Settings.Prop.ExperimentalRobloxHoldToSpamClicksPerSecond = clicksPerSecond;
                SaveAndRefresh(nameof(RobloxHoldToSpamClicksPerSecond));
            }
        }

        public bool RobloxHoldToSpamHandgunOnly
        {
            get => App.Settings.Prop.ExperimentalRobloxHoldToSpamHandgunOnly;
            set
            {
                if (value == App.Settings.Prop.ExperimentalRobloxHoldToSpamHandgunOnly)
                    return;

                App.Settings.Prop.ExperimentalRobloxHoldToSpamHandgunOnly = value;
                SaveAndRefresh(nameof(RobloxHoldToSpamHandgunOnly));
            }
        }

        /// <summary>
        /// Controls only user-imported skies. Preset skies remain available regardless of this setting.
        /// Importing a PNG never changes this value, so imported skies stay disabled by default.
        /// </summary>
        public bool CustomImportedSkyboxesEnabled
        {
            get => App.Settings.Prop.CustomImportedSkyboxesEnabled;
            set
            {
                if (value == App.Settings.Prop.CustomImportedSkyboxesEnabled)
                    return;

                App.Settings.Prop.CustomImportedSkyboxesEnabled = value;

                // If an imported sky is selected, disabling this master setting also
                // disables that selection for the next launch. Preset skies are left alone.
                if (!value &&
                    App.Settings.Prop.CustomSkyboxEnabled &&
                    UserSkyboxService.IsUserSkyboxKey(App.Settings.Prop.CustomSkyboxSourceName))
                {
                    App.Settings.Prop.CustomSkyboxEnabled = false;
                    App.Settings.Prop.CustomSkyboxSourceName = "";
                }

                App.Settings.Save();
                SkyboxGalleryService.NotifyGalleryChanged();
                OnPropertyChanged(nameof(CustomImportedSkyboxesEnabled));
                CustomSkyboxStatus = value
                    ? "Imported skyboxes are enabled. Pick one from Skybox when you are ready."
                    : "Imported skyboxes are disabled. Your saved skies stay here safely.";
            }
        }

        public string CustomSkyboxName
        {
            get => _customSkyboxName;
            set
            {
                string name = value ?? "";
                if (String.Equals(_customSkyboxName, name, StringComparison.Ordinal))
                    return;

                _customSkyboxName = name;
                OnPropertyChanged(nameof(CustomSkyboxName));
                ImportCustomSkyboxCommand.NotifyCanExecuteChanged();
                ImportCustomSkyboxTexturesCommand.NotifyCanExecuteChanged();
            }
        }

        public string CustomSkyboxSourcePath
        {
            get => _customSkyboxSourcePath;
            private set
            {
                if (String.Equals(_customSkyboxSourcePath, value, StringComparison.Ordinal))
                    return;

                _customSkyboxSourcePath = value;
                OnPropertyChanged(nameof(CustomSkyboxSourcePath));
                OnPropertyChanged(nameof(CustomSkyboxSourceDisplay));
                ImportCustomSkyboxCommand.NotifyCanExecuteChanged();
            }
        }

        public string CustomSkyboxSourceDisplay => String.IsNullOrWhiteSpace(CustomSkyboxSourcePath)
            ? "No PNG selected"
            : Path.GetFileName(CustomSkyboxSourcePath);

        public string CustomSkyboxTextureSetDisplay => _customSkyboxTexturePaths.Count == 0
            ? "Drop or choose all six sky512_*.tex files"
            : $"{_customSkyboxTexturePaths.Count}/6 texture files selected";

        public string CustomSkyboxStatus
        {
            get => _customSkyboxStatus;
            private set
            {
                if (String.Equals(_customSkyboxStatus, value, StringComparison.Ordinal))
                    return;

                _customSkyboxStatus = value;
                OnPropertyChanged(nameof(CustomSkyboxStatus));
            }
        }

        public bool IsImportingCustomSkybox
        {
            get => _isImportingCustomSkybox;
            private set
            {
                if (_isImportingCustomSkybox == value)
                    return;

                _isImportingCustomSkybox = value;
                OnPropertyChanged(nameof(IsImportingCustomSkybox));
                ChooseCustomSkyboxPngCommand.NotifyCanExecuteChanged();
                ImportCustomSkyboxCommand.NotifyCanExecuteChanged();
                ChooseCustomSkyboxTexturesCommand.NotifyCanExecuteChanged();
                ImportCustomSkyboxTexturesCommand.NotifyCanExecuteChanged();
            }
        }

        public string AutoClickerHotkeyDisplay => FormatHotkey(
            App.Settings.Prop.ExperimentalAutoClickerHotkeyModifiers,
            App.Settings.Prop.ExperimentalAutoClickerHotkeyVirtualKey);

        public string AutoClickerStatus => AutoClickerEnabled
            ? $"Ready — press {AutoClickerHotkeyDisplay} to toggle"
            : "Off";

        public void SetAutoClickerHotkey(int modifiers, int virtualKey)
        {
            App.Settings.Prop.ExperimentalAutoClickerHotkeyModifiers = modifiers;
            App.Settings.Prop.ExperimentalAutoClickerHotkeyVirtualKey = virtualKey;
            App.Settings.Save();
            ExperimentalClickerHostService.Refresh();
            OnPropertyChanged(nameof(AutoClickerHotkeyDisplay));
            OnPropertyChanged(nameof(AutoClickerStatus));
        }

        private void ChooseCustomSkyboxPng()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose a 2:1 panorama or 4x3 cube-cross sky PNG",
                Filter = "Sky PNG (*.png)|*.png"
            };

            if (dialog.ShowDialog() != true)
                return;

            CustomSkyboxSourcePath = dialog.FileName;
            if (String.IsNullOrWhiteSpace(CustomSkyboxName))
                CustomSkyboxName = Path.GetFileNameWithoutExtension(dialog.FileName);

            CustomSkyboxStatus = "Ready to save as a custom skybox. It will stay disabled until you enable imported skyboxes.";
        }

        private async Task ImportCustomSkyboxAsync()
        {
            string sourcePath = CustomSkyboxSourcePath;
            string displayName = CustomSkyboxName.Trim();
            if (String.IsNullOrWhiteSpace(displayName) || !File.Exists(sourcePath))
                return;

            try
            {
                IsImportingCustomSkybox = true;
                CustomSkyboxStatus = "Converting the PNG into six Roblox skybox faces…";

                // UserSkyboxService only stores the imported data under SleepStrap's data
                // directory. It deliberately does not touch Roblox or its modifications folder.
                await UserSkyboxService.ImportAsync(sourcePath, displayName);

                CustomSkyboxSourcePath = "";
                CustomSkyboxName = "";
                CustomSkyboxStatus = CustomImportedSkyboxesEnabled
                    ? $"{displayName} was added. You can select it in Skybox."
                    : $"{displayName} was added and remains disabled until you enable imported skyboxes.";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ExperimentalViewModel::ImportCustomSkybox", ex);
                Frontend.ShowMessageBox($"{App.ProjectName} could not import that skybox.\n\n{ex.Message}", MessageBoxImage.Error);
                CustomSkyboxStatus = "Skybox import failed.";
            }
            finally
            {
                IsImportingCustomSkybox = false;
            }
        }

        public void SetCustomSkyboxTextureFiles(IEnumerable<string> paths)
        {
            _customSkyboxTexturePaths = paths
                .Where(path => !String.IsNullOrWhiteSpace(path) &&
                               String.Equals(Path.GetExtension(path), ".tex", StringComparison.OrdinalIgnoreCase) &&
                               File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (String.IsNullOrWhiteSpace(CustomSkyboxName) && _customSkyboxTexturePaths.Count > 0)
                CustomSkyboxName = new DirectoryInfo(Path.GetDirectoryName(_customSkyboxTexturePaths[0])!).Name;

            OnPropertyChanged(nameof(CustomSkyboxTextureSetDisplay));
            ImportCustomSkyboxTexturesCommand.NotifyCanExecuteChanged();
            CustomSkyboxStatus = _customSkyboxTexturePaths.Count == 6
                ? "Ready to add the six Roblox texture faces. It will stay disabled until you enable imported skyboxes."
                : $"Select all six Roblox sky512 texture files ({_customSkyboxTexturePaths.Count}/6 selected).";
        }

        private void ChooseCustomSkyboxTextures()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose all six Roblox sky512 texture files",
                Filter = "Roblox sky textures (*.tex)|*.tex",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
                SetCustomSkyboxTextureFiles(dialog.FileNames);
        }

        private async Task ImportCustomSkyboxTexturesAsync()
        {
            string displayName = CustomSkyboxName.Trim();
            string[] texturePaths = _customSkyboxTexturePaths.ToArray();
            if (String.IsNullOrWhiteSpace(displayName) || texturePaths.Length != 6)
                return;

            try
            {
                IsImportingCustomSkybox = true;
                CustomSkyboxStatus = "Adding the six Roblox sky textures and generating their previewâ€¦";
                await UserSkyboxService.ImportTextureSetAsync(texturePaths, displayName);
                _customSkyboxTexturePaths = Array.Empty<string>();
                CustomSkyboxName = "";
                OnPropertyChanged(nameof(CustomSkyboxTextureSetDisplay));
                ImportCustomSkyboxTexturesCommand.NotifyCanExecuteChanged();
                CustomSkyboxStatus = CustomImportedSkyboxesEnabled
                    ? $"{displayName} was added. You can select it in Skybox."
                    : $"{displayName} was added and remains disabled until you enable imported skyboxes.";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ExperimentalViewModel::ImportCustomSkyboxTextures", ex);
                Frontend.ShowMessageBox($"{App.ProjectName} could not import that texture set.\n\n{ex.Message}", MessageBoxImage.Error);
                CustomSkyboxStatus = "Texture-set import failed.";
            }
            finally
            {
                IsImportingCustomSkybox = false;
            }
        }

        private static string FormatHotkey(int modifiers, int virtualKey)
        {
            var parts = new List<string>();
            if ((modifiers & 2) != 0) parts.Add("Ctrl");
            if ((modifiers & 1) != 0) parts.Add("Alt");
            if ((modifiers & 4) != 0) parts.Add("Shift");
            if ((modifiers & 8) != 0) parts.Add("Win");

            Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
            parts.Add(key == Key.None ? $"0x{virtualKey:X2}" : key.ToString());
            return String.Join(" + ", parts);
        }

        private void SaveAndRefresh(string propertyName)
        {
            App.Settings.Save();
            ExperimentalClickerHostService.Refresh();
            OnPropertyChanged(propertyName);
        }
    }
}
