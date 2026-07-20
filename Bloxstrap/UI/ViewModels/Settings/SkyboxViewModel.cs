using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Services;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class SkyboxViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isBusy;
        private SkyboxChoice? _selectedSkybox;
        private string _statusText;

        public SkyboxViewModel()
        {
            SkyboxChoices = SkyboxGalleryService.GetChoices();
            OpenModsFolderCommand = new RelayCommand(OpenModsFolder);

            if (!App.Settings.Prop.CustomSkyboxEnabled)
            {
                _selectedSkybox = SkyboxChoices.First(x => x.IsNone);
                _statusText = "Roblox's original sky is active.";
            }
            else
            {
                _selectedSkybox = SkyboxChoices.FirstOrDefault(x =>
                    !x.IsNone && String.Equals(x.Name, App.Settings.Prop.CustomSkyboxSourceName, StringComparison.OrdinalIgnoreCase));
                _statusText = _selectedSkybox is null
                    ? "A legacy imported sky is active. Pick a preset or None to replace it."
                    : $"Selected: {_selectedSkybox.Name}.";
            }
        }

        public IReadOnlyList<SkyboxChoice> SkyboxChoices { get; }
        public ICommand OpenModsFolderCommand { get; }

        public SkyboxChoice? SelectedSkybox
        {
            get => _selectedSkybox;
            set
            {
                if (value is null || value == _selectedSkybox || IsBusy)
                    return;

                SkyboxChoice? previous = _selectedSkybox;
                try
                {
                    IsBusy = true;

                    if (value.IsNone)
                    {
                        int closedProcesses = VisualModService.CloseRobloxProcesses();
                        VisualModService.RemoveCustomSkybox();
                        App.Settings.Prop.CustomSkyboxEnabled = false;
                        App.Settings.Prop.CustomSkyboxSourceName = "";
                        StatusText = App.Settings.Prop.DarkTexturesEnabled
                            ? "No gallery sky selected. The dark texture pack sky is active."
                            : closedProcesses > 0
                                ? $"Roblox was closed ({closedProcesses} processes). None selected; the original sky will be restored on launch."
                                : "None selected. Roblox's original sky will be restored on launch.";
                    }
                    else
                    {
                        if (!EnsureRiskAcknowledged())
                        {
                            OnPropertyChanged(nameof(SelectedSkybox));
                            return;
                        }

                        int closedProcesses = VisualModService.CloseRobloxProcesses();
                        VisualModService.ApplyEmbeddedSkybox(value.ResourceFolder);
                        int updatedVersions = VisualModService.DeployCachedSkyboxToInstalledVersions();
                        int patchedRivalsAssets = VisualModService.ApplyRivalsSkyboxCompatibilityFix();
                        App.Settings.Prop.CustomSkyboxEnabled = true;
                        App.Settings.Prop.CustomSkyboxSourceName = value.Name;
                        StatusText = updatedVersions > 0
                            ? $"Selected: {value.Name}. Replaced six sky files in {updatedVersions} Roblox version{(updatedVersions == 1 ? "" : "s")} and patched {patchedRivalsAssets} RIVALS sky assets{(closedProcesses > 0 ? $" after closing {closedProcesses} Roblox process{(closedProcesses == 1 ? "" : "es")}" : "")}."
                            : $"Selected: {value.Name}. Six DDS sky files are ready and will be installed on the next Roblox launch.";
                    }

                    _selectedSkybox = value;
                    App.Settings.Save();
                    OnPropertyChanged(nameof(SelectedSkybox));
                }
                catch (Exception ex)
                {
                    _selectedSkybox = previous;
                    App.Logger.WriteException("SkyboxViewModel::SelectSkybox", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not apply that sky.\n\n{ex.Message}", MessageBoxImage.Error);
                    StatusText = "Sky selection failed.";
                    OnPropertyChanged(nameof(SelectedSkybox));
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private static void OpenModsFolder()
        {
            Directory.CreateDirectory(Paths.Modifications);
            Process.Start("explorer.exe", Paths.Modifications);
        }

        private static bool EnsureRiskAcknowledged()
        {
            if (App.State.Prop.VisualModsWarningAcknowledged)
                return true;

            const string message = "Skybox presets replace local Roblox asset files. They do not inject code or modify the Roblox executable, but they are unofficial and SleepStrap cannot guarantee that Roblox will never take enforcement action.\n\nContinue with visual mods?";
            if (Frontend.ShowMessageBox(message, MessageBoxImage.Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return false;

            App.State.Prop.VisualModsWarningAcknowledged = true;
            App.State.Save();
            return true;
        }
    }
}
