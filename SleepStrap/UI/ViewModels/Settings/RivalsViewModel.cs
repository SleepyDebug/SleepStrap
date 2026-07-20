using System.Windows;

using SleepStrap.Services;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class RivalsViewModel : NotifyPropertyChangedViewModel
    {
        private const string MissingFlagValue = "__SLEEPSTRAP_FLAG_WAS_MISSING__";
        private const string TargetFpsFlag = "DFIntTaskSchedulerTargetFps";
        private const string UnlockFpsFlag = "FFlagTaskSchedulerLimitTargetFpsTo2402";
        private const string DisplayFpsFlag = "FFlagDebugDisplayFPS";

        public sealed record StretchPreset(string Name, int Percent)
        {
            public override string ToString() => Name;
        }

        public sealed record FpsPreset(string Name, int Limit)
        {
            public override string ToString() => Name;
        }

        public IReadOnlyList<StretchPreset> StretchPresets { get; } = new[]
        {
            new StretchPreset("Mild — 85% width", 85),
            new StretchPreset("Balanced — 75% width", 75),
            new StretchPreset("Strong — 67% width", 67)
        };

        public IReadOnlyList<FpsPreset> FpsPresets { get; } = new[]
        {
            new FpsPreset("Roblox default", 0),
            new FpsPreset("60 FPS", 60),
            new FpsPreset("120 FPS", 120),
            new FpsPreset("144 FPS", 144),
            new FpsPreset("165 FPS", 165),
            new FpsPreset("240 FPS", 240),
            new FpsPreset("360 FPS", 360),
            new FpsPreset("480 FPS", 480),
            new FpsPreset("Unlimited", 9999)
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

        public int SelectedFpsLimit
        {
            get => App.Settings.Prop.RivalsFpsLimit;
            set
            {
                if (!FpsPresets.Any(preset => preset.Limit == value) || value == App.Settings.Prop.RivalsFpsLimit)
                    return;

                try
                {
                    if (value == 0)
                        RestoreFpsFlags();
                    else
                        ApplyFpsFlags(value);

                    App.Settings.Prop.RivalsFpsLimit = value;
                    App.Settings.Prop.UseFastFlagManager = true;
                    App.FastFlags.Save();
                    App.Settings.Save();
                    OnPropertyChanged(nameof(SelectedFpsLimit));
                    RefreshStatus();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("RivalsViewModel::ChangeFps", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not change the FPS limit.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(SelectedFpsLimit));
                }
            }
        }

        public bool FpsCounterEnabled
        {
            get => App.Settings.Prop.RivalsFpsCounterEnabled;
            set
            {
                if (value == App.Settings.Prop.RivalsFpsCounterEnabled)
                    return;

                try
                {
                    if (value)
                        EnableFpsCounter();
                    else
                        DisableFpsCounter();

                    App.Settings.Prop.RivalsFpsCounterEnabled = value;
                    App.Settings.Prop.UseFastFlagManager = true;
                    App.FastFlags.Save();
                    App.Settings.Save();
                    OnPropertyChanged(nameof(FpsCounterEnabled));
                    RefreshStatus();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("RivalsViewModel::ChangeFpsCounter", ex);
                    Frontend.ShowMessageBox($"SleepStrap could not change the FPS counter.\n\n{ex.Message}", MessageBoxImage.Error);
                    OnPropertyChanged(nameof(FpsCounterEnabled));
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

        private static void ApplyFpsFlags(int limit)
        {
            Dictionary<string, string> backup = App.Settings.Prop.RivalsFpsFlagBackup;
            if (!backup.ContainsKey(TargetFpsFlag))
                backup[TargetFpsFlag] = App.FastFlags.GetValue(TargetFpsFlag) ?? MissingFlagValue;
            if (!backup.ContainsKey(UnlockFpsFlag))
                backup[UnlockFpsFlag] = App.FastFlags.GetValue(UnlockFpsFlag) ?? MissingFlagValue;

            App.FastFlags.SetValue(TargetFpsFlag, limit.ToString());
            App.FastFlags.SetValue(UnlockFpsFlag, "False");
        }

        private static void RestoreFpsFlags()
        {
            Dictionary<string, string> backup = App.Settings.Prop.RivalsFpsFlagBackup;
            foreach (string key in new[] { TargetFpsFlag, UnlockFpsFlag })
            {
                if (!backup.TryGetValue(key, out string? previous))
                    continue;

                string ownedValue = key == TargetFpsFlag
                    ? App.Settings.Prop.RivalsFpsLimit.ToString()
                    : "False";
                if (String.Equals(App.FastFlags.GetValue(key), ownedValue, StringComparison.Ordinal))
                    App.FastFlags.SetValue(key, previous == MissingFlagValue ? null : previous);
            }

            backup.Clear();
        }

        private static void EnableFpsCounter()
        {
            Dictionary<string, string> backup = App.Settings.Prop.RivalsFpsCounterFlagBackup;
            if (!backup.ContainsKey(DisplayFpsFlag))
                backup[DisplayFpsFlag] = App.FastFlags.GetValue(DisplayFpsFlag) ?? MissingFlagValue;

            App.FastFlags.SetValue(DisplayFpsFlag, "True");
        }

        private static void DisableFpsCounter()
        {
            Dictionary<string, string> backup = App.Settings.Prop.RivalsFpsCounterFlagBackup;
            if (backup.TryGetValue(DisplayFpsFlag, out string? previous) &&
                String.Equals(App.FastFlags.GetValue(DisplayFpsFlag), "True", StringComparison.Ordinal))
            {
                App.FastFlags.SetValue(DisplayFpsFlag, previous == MissingFlagValue ? null : previous);
            }

            backup.Clear();
        }

        private void RefreshStatus()
        {
            try
            {
                DisplayStretchService.DisplayMode current = DisplayStretchService.GetCurrentMode();
                StatusText = StretchEnabled
                    ? $"Stretch active: {current}. {GetFpsStatus()}"
                    : $"Normal display: {current}. {GetFpsStatus()}";
            }
            catch
            {
                StatusText = $"{(StretchEnabled ? "Stretch is marked active." : "Stretch is off.")} {GetFpsStatus()}";
            }
        }

        private static string GetFpsStatus() => App.Settings.Prop.RivalsFpsLimit switch
        {
            0 => $"FPS uses the Roblox default.{GetCounterStatus()}",
            9999 => $"FPS is unlimited.{GetCounterStatus()}",
            int limit => $"FPS limit: {limit}.{GetCounterStatus()}"
        };

        private static string GetCounterStatus() => App.Settings.Prop.RivalsFpsCounterEnabled
            ? " Counter: top right."
            : "";
    }
}
