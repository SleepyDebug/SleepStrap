using System.Windows;

using Bloxstrap.Services;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class RivalsViewModel : NotifyPropertyChangedViewModel
    {
        public sealed record StretchPreset(string Name, int Percent)
        {
            public override string ToString() => Name;
        }

        public IReadOnlyList<StretchPreset> StretchPresets { get; } = new[]
        {
            new StretchPreset("Mild — 85% width", 85),
            new StretchPreset("Balanced — 75% width", 75),
            new StretchPreset("Strong — 67% width", 67)
        };

        public bool StretchEnabled
        {
            get => App.Settings.Prop.RivalsStretchEnabled;
            set
            {
                if (value == App.Settings.Prop.RivalsStretchEnabled)
                    return;

                if (value && Frontend.ShowMessageBox(
                    "Rivals stretch temporarily changes the primary Windows display resolution. This affects the whole desktop until you turn it off. It does not inject into Roblox.\n\nFor the stretched result, set your GPU scaling mode to Full-screen. Continue?",
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    OnPropertyChanged(nameof(StretchEnabled));
                    return;
                }

                try
                {
                    if (value)
                        EnableStretch();
                    else
                        DisableStretch();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("RivalsViewModel::SetStretch", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not change the display stretch.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(StretchEnabled));
                    RefreshStatus();
                }
            }
        }

        public int SelectedStretchPercent
        {
            get => App.Settings.Prop.RivalsStretchPercent;
            set
            {
                if (value == App.Settings.Prop.RivalsStretchPercent)
                    return;

                int previous = App.Settings.Prop.RivalsStretchPercent;
                App.Settings.Prop.RivalsStretchPercent = value;

                try
                {
                    if (StretchEnabled)
                        ApplySavedStretch();

                    App.Settings.Save();
                    OnPropertyChanged(nameof(SelectedStretchPercent));
                    RefreshStatus();
                }
                catch (Exception ex)
                {
                    App.Settings.Prop.RivalsStretchPercent = previous;
                    App.Logger.WriteException("RivalsViewModel::ChangeStrength", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not apply that stretch strength.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(SelectedStretchPercent));
                    RefreshStatus();
                }
            }
        }

        private string _statusText = "Checking display…";
        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public RivalsViewModel() => RefreshStatus();

        private void EnableStretch()
        {
            DisplayStretchService.DisplayMode native = DisplayStretchService.GetCurrentMode();
            App.Settings.Prop.RivalsNativeWidth = native.Width;
            App.Settings.Prop.RivalsNativeHeight = native.Height;
            App.Settings.Prop.RivalsNativeFrequency = native.Frequency;

            DisplayStretchService.DisplayMode applied = DisplayStretchService.ApplyHorizontalStretch(
                App.Settings.Prop.RivalsStretchPercent,
                native);

            App.Settings.Prop.RivalsStretchEnabled = true;
            App.Settings.Save();
            OnPropertyChanged(nameof(StretchEnabled));
            StatusText = $"Stretch active: {applied}. Turn the toggle off to restore {native}.";
        }

        private void DisableStretch()
        {
            DisplayStretchService.DisplayMode native = GetSavedNativeMode();
            DisplayStretchService.Restore(native);
            App.Settings.Prop.RivalsStretchEnabled = false;
            App.Settings.Save();
            OnPropertyChanged(nameof(StretchEnabled));
            RefreshStatus();
        }

        private void ApplySavedStretch()
        {
            DisplayStretchService.DisplayMode native = GetSavedNativeMode();
            DisplayStretchService.ApplyHorizontalStretch(App.Settings.Prop.RivalsStretchPercent, native);
        }

        private static DisplayStretchService.DisplayMode GetSavedNativeMode() => new(
            App.Settings.Prop.RivalsNativeWidth,
            App.Settings.Prop.RivalsNativeHeight,
            App.Settings.Prop.RivalsNativeFrequency);

        private void RefreshStatus()
        {
            try
            {
                DisplayStretchService.DisplayMode current = DisplayStretchService.GetCurrentMode();
                StatusText = StretchEnabled
                    ? $"Stretch active: {current}. Turn the toggle off to restore the normal resolution."
                    : $"Normal display: {current}.";
            }
            catch
            {
                StatusText = StretchEnabled ? "Stretch is marked active." : "Stretch is off.";
            }
        }
    }
}
