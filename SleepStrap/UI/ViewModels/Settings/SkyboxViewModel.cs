using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class SkyboxViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isBusy;
        private SkyboxChoice? _selectedSkybox;
        private string _statusText = "";

        public SkyboxViewModel()
        {
            SkyboxChoices = new ObservableCollection<SkyboxChoice>();
            OpenModsFolderCommand = new RelayCommand(OpenModsFolder);

            ReloadChoices();
            SkyboxGalleryService.GalleryChanged += SkyboxGalleryService_GalleryChanged;
        }

        public ObservableCollection<SkyboxChoice> SkyboxChoices { get; }
        public ICommand OpenModsFolderCommand { get; }

        public void ToggleFavorite(SkyboxChoice? choice)
        {
            if (choice is null || choice.IsNone || IsBusy)
                return;

            choice.IsFavorite = !choice.IsFavorite;
            App.Settings.Prop.FavoriteSkyboxes = SkyboxChoices
                .Where(x => x.IsFavorite)
                .Select(x => x.SelectionKey)
                .ToList();
            App.Settings.Save();
            ReorderFavoritesAndOriginals();
        }

        private void SkyboxGalleryService_GalleryChanged(object? sender, EventArgs e)
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                _ = dispatcher.BeginInvoke(new Action(ReloadChoices));
                return;
            }

            ReloadChoices();
        }

        private void ReloadChoices()
        {
            string selectedKey = App.Settings.Prop.CustomSkyboxSourceName ?? "";
            List<SkyboxChoice> choices = SkyboxGalleryService.GetChoices().ToList();

            for (int index = 0; index < choices.Count; index++)
            {
                SkyboxChoice choice = choices[index];
                choice.OriginalIndex = index;
                choice.IsFavorite = !choice.IsNone && App.Settings.Prop.FavoriteSkyboxes.Any(favorite =>
                    String.Equals(favorite, choice.SelectionKey, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(favorite, choice.Name, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(favorite, choice.ResourceFolder, StringComparison.OrdinalIgnoreCase));
            }

            SkyboxChoices.Clear();
            foreach (SkyboxChoice choice in choices)
                SkyboxChoices.Add(choice);
            ReorderFavoritesAndOriginals();

            SkyboxChoice none = SkyboxChoices.First(x => x.IsNone);
            if (!App.Settings.Prop.CustomSkyboxEnabled)
            {
                _selectedSkybox = none;
                _statusText = "Roblox's original sky is active.";
            }
            else
            {
                _selectedSkybox = SkyboxChoices.FirstOrDefault(choice =>
                    !choice.IsNone && MatchesSelection(choice, selectedKey));

                if (_selectedSkybox?.IsUserImported == true && !_selectedSkybox.IsAvailable)
                {
                    _selectedSkybox = none;
                    _statusText = UserSkyboxService.IsEnabled
                        ? "This imported sky needs to be re-imported with the updated Roblox texture format."
                        : "Imported skyboxes are disabled in Experimental.";
                }
                else
                {
                    _statusText = _selectedSkybox is null
                        ? "A legacy imported sky is active. Pick a preset or None to replace it."
                        : $"Selected: {_selectedSkybox.Name}.";
                }
            }

            OnPropertyChanged(nameof(SelectedSkybox));
            OnPropertyChanged(nameof(StatusText));
        }

        private static bool MatchesSelection(SkyboxChoice choice, string selection) =>
            String.Equals(choice.SelectionKey, selection, StringComparison.OrdinalIgnoreCase) ||
            String.Equals(choice.Name, selection, StringComparison.OrdinalIgnoreCase) ||
            String.Equals(choice.ResourceFolder, selection, StringComparison.OrdinalIgnoreCase);

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

                if (value.IsUserImported && !value.IsAvailable)
                {
                    StatusText = UserSkyboxService.IsEnabled
                        ? "Re-import this sky to update it to Roblox's current texture format."
                        : "Enable imported skyboxes in Experimental before selecting one.";
                    OnPropertyChanged(nameof(SelectedSkybox));
                    return;
                }

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
                        App.Settings.Prop.CustomSkyboxSourceName = value.SelectionKey;
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
                    Frontend.ShowMessageBox($"{App.ProjectName} could not apply that sky.\n\n{ex.Message}", MessageBoxImage.Error);
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

            const string message = "Skybox presets replace local Roblox asset files. They do not inject code or modify the Roblox executable, but they are unofficial and SleepBlox cannot guarantee that Roblox will never take enforcement action.\n\nContinue with visual mods?";
            if (Frontend.ShowMessageBox(message, MessageBoxImage.Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return false;

            App.State.Prop.VisualModsWarningAcknowledged = true;
            App.State.Save();
            return true;
        }
    }
}
