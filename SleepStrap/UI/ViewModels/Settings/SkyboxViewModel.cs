using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class SkyboxViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isBusy;
        private SkyboxChoice? _selectedSkybox;
        private string _statusText;

        public SkyboxViewModel()
        {
            SkyboxChoices = new ObservableCollection<SkyboxChoice>(SkyboxGalleryService.GetChoices());
            for (int index = 0; index < SkyboxChoices.Count; index++)
                SkyboxChoices[index].OriginalIndex = index;
            foreach (SkyboxChoice choice in SkyboxChoices)
                choice.IsFavorite = !choice.IsNone && App.Settings.Prop.FavoriteSkyboxes.Any(x =>
                    String.Equals(x, choice.Name, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(x, choice.ResourceFolder, StringComparison.OrdinalIgnoreCase));
            ReorderFavoritesAndOriginals();
            OpenModsFolderCommand = new RelayCommand(OpenModsFolder);

            if (!App.Settings.Prop.CustomSkyboxEnabled)
            {
                _selectedSkybox = SkyboxChoices.First(x => x.IsNone);
                _statusText = "Roblox's original sky is active.";
            }
            else
            {
                _selectedSkybox = SkyboxChoices.FirstOrDefault(x =>
                    !x.IsNone &&
                    (String.Equals(x.Name, App.Settings.Prop.CustomSkyboxSourceName, StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(x.ResourceFolder, App.Settings.Prop.CustomSkyboxSourceName, StringComparison.OrdinalIgnoreCase)));
                _statusText = _selectedSkybox is null
                    ? "A legacy imported sky is active. Pick a preset or None to replace it."
                    : $"Selected: {_selectedSkybox.Name}.";
            }
        }

        public ObservableCollection<SkyboxChoice> SkyboxChoices { get; }
        public ICommand OpenModsFolderCommand { get; }

        public void ToggleFavorite(SkyboxChoice? choice)
        {
            if (choice is null || choice.IsNone || IsBusy)
                return;

            choice.IsFavorite = !choice.IsFavorite;
            App.Settings.Prop.FavoriteSkyboxes = SkyboxChoices.Where(x => x.IsFavorite).Select(x => x.Name).ToList();
            App.Settings.Save();
            ReorderFavoritesAndOriginals();
        }

        private void ReorderFavoritesAndOriginals()
        {
            List<SkyboxChoice> ordered = SkyboxChoices
                .OrderByDescending(x => x.IsFavorite)
                .ThenBy(x => x.OriginalIndex)
                .ToList();
            for (int target = 0; target < ordered.Count; target++)
            {
                int index = SkyboxChoices.IndexOf(ordered[target]);
                if (index >= 0 && index != target)
                    SkyboxChoices.Move(index, target);
            }
        }

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
                        App.Settings.Prop.CustomSkyboxEnabled = false;
                        App.Settings.Prop.CustomSkyboxSourceName = "";
                        StatusText = "None selected. The original sky will be restored when you click Launch Roblox.";
                    }
                    else
                    {
                        if (!EnsureRiskAcknowledged())
                        {
                            OnPropertyChanged(nameof(SelectedSkybox));
                            return;
                        }

                        App.Settings.Prop.CustomSkyboxEnabled = true;
                        App.Settings.Prop.CustomSkyboxSourceName = value.Name;
                        StatusText = $"Selected: {value.Name}. It will be applied when you click Launch Roblox.";
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
