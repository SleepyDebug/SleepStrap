using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class VisualModsViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isBusy;
        private string _statusText = "Ready";
        private readonly List<FontChoice> _fontChoices;
        private FontChoice? _selectedFont;
        private string _fontStatus = "Roblox default font";

        public VisualModsViewModel()
        {
            ImportSkyboxCommand = new AsyncRelayCommand(ImportSkyboxAsync, () => !IsBusy);
            RemoveSkyboxCommand = new RelayCommand(RemoveSkybox, () => !IsBusy && CustomSkyboxEnabled);
            OpenModsFolderCommand = new RelayCommand(OpenModsFolder);
            RestoreDefaultFontCommand = new RelayCommand(RestoreDefaultFont, () => !IsBusy && _selectedFont?.IsDefault == false);

            _fontChoices = FontModService.GetAvailableFonts().ToList();
            _selectedFont = File.Exists(Paths.CustomFont)
                ? _fontChoices.FirstOrDefault(x =>
                    !x.IsDefault &&
                    (String.Equals(x.DisplayName, App.Settings.Prop.SelectedFontName, StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(x.FilePath, App.Settings.Prop.SelectedFontSource, StringComparison.OrdinalIgnoreCase)))
                : _fontChoices.FirstOrDefault(x => x.IsDefault);
            _selectedFont ??= _fontChoices.FirstOrDefault(x => x.IsDefault);
            _fontStatus = _selectedFont?.IsDefault == false
                ? $"Selected: {_selectedFont.DisplayName} — applies on the next Roblox launch."
                : "Roblox default font is active.";

        }

        public IAsyncRelayCommand ImportSkyboxCommand { get; }
        public IRelayCommand RemoveSkyboxCommand { get; }
        public ICommand OpenModsFolderCommand { get; }
        public IRelayCommand RestoreDefaultFontCommand { get; }

        public IReadOnlyList<FontChoice> FontChoices => _fontChoices;

        public FontChoice? SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (value is null || value == _selectedFont || IsBusy)
                    return;

                try
                {
                    IsBusy = true;
                    _selectedFont = FontModService.ApplyFont(value);
                    FontStatus = _selectedFont.IsDefault
                        ? "Roblox default font restored."
                        : $"Selected: {_selectedFont.DisplayName} — applies on the next Roblox launch.";
                    StatusText = FontStatus;
                    OnPropertyChanged(nameof(SelectedFont));
                    OnPropertyChanged(nameof(RestoreFontVisibility));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("VisualModsViewModel::SelectFont", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not apply that font.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(SelectedFont));
                }
                finally
                {
                    IsBusy = false;
                    RestoreDefaultFontCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string FontStatus
        {
            get => _fontStatus;
            private set
            {
                _fontStatus = value;
                OnPropertyChanged(nameof(FontStatus));
            }
        }

        public Visibility RestoreFontVisibility => _selectedFont?.IsDefault == false ? Visibility.Visible : Visibility.Collapsed;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                ImportSkyboxCommand.NotifyCanExecuteChanged();
                RemoveSkyboxCommand.NotifyCanExecuteChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public bool DarkTexturesEnabled
        {
            get => App.Settings.Prop.DarkTexturesEnabled;
            set
            {
                if (value == App.Settings.Prop.DarkTexturesEnabled || IsBusy)
                    return;
                if (!EnsureRiskAcknowledged())
                {
                    OnPropertyChanged(nameof(DarkTexturesEnabled));
                    return;
                }
                try
                {
                    IsBusy = true;
                    StatusText = value ? "Installing dark textures…" : "Restoring basic textures…";
                    VisualModService.SetDarkTextures(value);
                    App.Settings.Prop.DarkTexturesEnabled = value;
                    App.Settings.Save();
                    StatusText = value ? "Dark textures are ready for the next launch." : "Basic Roblox textures restored.";
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("VisualModsViewModel::SetDarkTextures", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not switch the texture pack.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(DarkTexturesEnabled));
                    StatusText = "Texture switch failed.";
                }
                finally { IsBusy = false; }
            }
        }

        public bool BlurryTexturesEnabled
        {
            get => App.Settings.Prop.BlurryTexturesEnabled;
            set
            {
                if (value == App.Settings.Prop.BlurryTexturesEnabled || IsBusy)
                    return;

                try
                {
                    IsBusy = true;
                    StatusText = value ? "Applying blurry texture quality…" : "Restoring previous texture quality…";
                    BlurryTextureService.SetEnabled(value);
                    App.Settings.Prop.BlurryTexturesEnabled = value;
                    App.Settings.Save();
                    StatusText = value
                        ? "Blurry texture quality is ready for the next launch."
                        : "Previous texture-quality flags restored.";
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("VisualModsViewModel::SetBlurryTextures", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not change blurry textures.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(BlurryTexturesEnabled));
                    StatusText = "Blurry texture switch failed.";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        public bool RtxShineEnabled
        {
            get => App.Settings.Prop.RtxShineEnabled;
            set
            {
                if (value == App.Settings.Prop.RtxShineEnabled || IsBusy)
                    return;
                if (!EnsureRiskAcknowledged())
                {
                    OnPropertyChanged(nameof(RtxShineEnabled));
                    return;
                }

                try
                {
                    IsBusy = true;
                    StatusText = value ? "Applying RTX shine…" : "Restoring normal lighting…";
                    VisualModService.SetRtxShine(value);
                    App.Settings.Prop.RtxShineEnabled = value;
                    App.Settings.Save();
                    StatusText = value
                        ? "RTX shine is ready for the next launch."
                        : "Normal lighting restored.";
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("VisualModsViewModel::SetRtxShine", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not change RTX shine.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(RtxShineEnabled));
                    StatusText = "RTX shine switch failed.";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        public IReadOnlyList<string> TextureEffectChoices { get; } = new[] { "None", "Blurry", "RTX" };

        public string SelectedTextureEffect
        {
            get => App.Settings.Prop.RtxShineEnabled
                ? "RTX"
                : App.Settings.Prop.BlurryTexturesEnabled ? "Blurry" : "None";
            set
            {
                if (!TextureEffectChoices.Contains(value) || IsBusy)
                    return;

                bool oldBlur = App.Settings.Prop.BlurryTexturesEnabled;
                bool oldRtx = App.Settings.Prop.RtxShineEnabled;
                bool enableBlur = String.Equals(value, "Blurry", StringComparison.Ordinal);
                bool enableRtx = String.Equals(value, "RTX", StringComparison.Ordinal);

                if (oldBlur == enableBlur && oldRtx == enableRtx)
                    return;
                if (enableRtx && !oldRtx && !EnsureRiskAcknowledged())
                {
                    OnPropertyChanged(nameof(SelectedTextureEffect));
                    return;
                }

                try
                {
                    IsBusy = true;
                    StatusText = enableBlur
                        ? "Applying blurry textures…"
                        : enableRtx ? "Applying RTX shine…" : "Restoring normal textures…";

                    if (oldBlur != enableBlur)
                        BlurryTextureService.SetEnabled(enableBlur);
                    if (oldRtx != enableRtx)
                        VisualModService.SetRtxShine(enableRtx);

                    App.Settings.Prop.BlurryTexturesEnabled = enableBlur;
                    App.Settings.Prop.RtxShineEnabled = enableRtx;
                    App.Settings.Save();
                    StatusText = enableBlur
                        ? "Blurry is ready for the next launch."
                        : enableRtx ? "RTX is ready for the next launch." : "None selected.";
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("VisualModsViewModel::SetTextureEffect", ex);
                    try
                    {
                        BlurryTextureService.SetEnabled(oldBlur);
                        VisualModService.SetRtxShine(oldRtx);
                    }
                    catch (Exception rollbackException)
                    {
                        App.Logger.WriteException("VisualModsViewModel::RollbackTextureEffect", rollbackException);
                    }

                    Frontend.ShowMessageBox($"SleepStrap could not change the texture effect.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(SelectedTextureEffect));
                    StatusText = "Texture effect failed.";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        public bool CustomSkyboxEnabled => App.Settings.Prop.CustomSkyboxEnabled && VisualModService.HasCachedSkybox;
        public string SkyboxStatus => CustomSkyboxEnabled ? $"Installed: {App.Settings.Prop.CustomSkyboxSourceName}" : "No custom skybox installed";
        public Visibility RemoveSkyboxVisibility => CustomSkyboxEnabled ? Visibility.Visible : Visibility.Collapsed;

        private async Task ImportSkyboxAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose a 2:1 sky panorama",
                Filter = "Sky images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true || !EnsureRiskAcknowledged())
                return;
            try
            {
                IsBusy = true;
                StatusText = "Building six Roblox skybox faces…";
                await VisualModService.ImportSkyboxAsync(dialog.FileName);
                App.Settings.Prop.CustomSkyboxEnabled = true;
                App.Settings.Prop.CustomSkyboxSourceName = Path.GetFileName(dialog.FileName);
                App.Settings.Save();
                StatusText = "Custom skybox installed for the next launch.";
                RefreshSkyboxState();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VisualModsViewModel::ImportSkybox", ex);
                Frontend.ShowMessageBox($"SleepStrap could not convert that panorama.\n\n{ex.Message}", MessageBoxImage.Error);
                StatusText = "Skybox import failed.";
            }
            finally { IsBusy = false; }
        }

        private void RemoveSkybox()
        {
            try
            {
                IsBusy = true;
                StatusText = "Restoring the previous skybox…";
                VisualModService.RemoveCustomSkybox();
                App.Settings.Prop.CustomSkyboxEnabled = false;
                App.Settings.Prop.CustomSkyboxSourceName = "";
                App.Settings.Save();
                StatusText = App.Settings.Prop.DarkTexturesEnabled ? "Dark texture pack sky restored." : "Basic Roblox sky restored.";
                RefreshSkyboxState();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VisualModsViewModel::RemoveSkybox", ex);
                Frontend.ShowMessageBox($"SleepStrap could not restore the previous skybox.\n\n{ex.Message}", MessageBoxImage.Error);
                StatusText = "Skybox restore failed.";
            }
            finally { IsBusy = false; }
        }

        private void RefreshSkyboxState()
        {
            OnPropertyChanged(nameof(CustomSkyboxEnabled));
            OnPropertyChanged(nameof(SkyboxStatus));
            OnPropertyChanged(nameof(RemoveSkyboxVisibility));
            RemoveSkyboxCommand.NotifyCanExecuteChanged();
        }

        private static void OpenModsFolder()
        {
            Directory.CreateDirectory(Paths.Modifications);
            Process.Start("explorer.exe", Paths.Modifications);
        }

        private void RestoreDefaultFont()
        {
            try
            {
                IsBusy = true;
                FontModService.RestoreDefault();
                _selectedFont = _fontChoices.FirstOrDefault(x => x.IsDefault);
                OnPropertyChanged(nameof(SelectedFont));
                OnPropertyChanged(nameof(RestoreFontVisibility));
                FontStatus = "Roblox default font restored.";
                StatusText = FontStatus;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VisualModsViewModel::RestoreFont", ex);
                Frontend.ShowMessageBox($"SleepStrap could not restore the Roblox font.\n\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RestoreDefaultFontCommand.NotifyCanExecuteChanged();
            }
        }

        private static bool EnsureRiskAcknowledged()
        {
            if (App.State.Prop.VisualModsWarningAcknowledged)
                return true;
            const string message = "Custom skyboxes and textures replace local Roblox asset files. They do not inject code or modify the Roblox executable, but they are unofficial and SleepStrap cannot guarantee that Roblox will never take enforcement action.\n\nContinue with visual mods?";
            if (Frontend.ShowMessageBox(message, MessageBoxImage.Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return false;
            App.State.Prop.VisualModsWarningAcknowledged = true;
            App.State.Save();
            return true;
        }
    }
}
