using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using Forms = System.Windows.Forms;

namespace SleepStrap.UI.ViewModels.Settings
{
    public sealed class ClippingViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        public sealed record DeviceChoice(string Name, string Id)
        {
            public override string ToString() => Name;
        }

        public sealed class ClipItem : NotifyPropertyChangedViewModel
        {
            private bool _isRenaming;
            private string _editName;

            public string FullPath { get; }
            public Uri MediaUri => new(FullPath);
            public string FileName => Path.GetFileName(FullPath);
            public string DisplayName => Path.GetFileNameWithoutExtension(FullPath);
            public string Details { get; }

            public bool IsRenaming
            {
                get => _isRenaming;
                set { _isRenaming = value; OnPropertyChanged(nameof(IsRenaming)); OnPropertyChanged(nameof(IsNotRenaming)); }
            }

            public bool IsNotRenaming => !IsRenaming;

            public string EditName
            {
                get => _editName;
                set { _editName = value; OnPropertyChanged(nameof(EditName)); }
            }

            public ClipItem(string path)
            {
                FullPath = path;
                _editName = DisplayName;
                var info = new FileInfo(path);
                Details = $"{info.LastWriteTime:g}  •  {FormatBytes(info.Length)}";
            }

            private static string FormatBytes(long bytes) => bytes >= 1_073_741_824
                ? $"{bytes / 1_073_741_824d:0.0} GB"
                : $"{bytes / 1_048_576d:0} MB";
        }

        private readonly FileSystemWatcher _clipWatcher;
        private readonly DispatcherTimer _liveRefreshTimer;
        private bool _disposed;
        private int _refreshPending;
        private string _clipSnapshot = String.Empty;
        private string _statusText = "Disabled";

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".mov", ".avi", ".wmv"
        };

        public ObservableCollection<ClipItem> Clips { get; } = new();
        public IReadOnlyList<DeviceChoice> Displays { get; }
        public IReadOnlyList<DeviceChoice> Speakers { get; }
        public IReadOnlyList<DeviceChoice> Microphones { get; }

        public bool ClippingEnabled
        {
            get => App.Settings.Prop.ClippingEnabled;
            set
            {
                if (value == App.Settings.Prop.ClippingEnabled)
                    return;
                App.Settings.Prop.ClippingEnabled = value;
                App.Settings.Save();
                if (value)
                    Services.ClippingHostService.EnsureStarted();
                else
                    Services.ClippingHostService.Stop();
                StatusText = value ? "Ready — Alt + X is active system-wide" : "Disabled";
                OnPropertyChanged(nameof(ClippingEnabled));
                OnPropertyChanged(nameof(AdvancedVisibility));
            }
        }

        public Visibility AdvancedVisibility => ClippingEnabled ? Visibility.Visible : Visibility.Collapsed;

        public int BufferMinutes
        {
            get => Math.Clamp(App.Settings.Prop.ClippingBufferMinutes, 1, 10);
            set
            {
                int clamped = Math.Clamp(value, 1, 10);
                if (clamped == App.Settings.Prop.ClippingBufferMinutes)
                    return;
                App.Settings.Prop.ClippingBufferMinutes = clamped;
                SaveAndNotify(nameof(BufferMinutes));
            }
        }

        public string HotkeyDisplay => FormatHotkey(App.Settings.Prop.ClippingHotkeyModifiers, App.Settings.Prop.ClippingHotkeyVirtualKey);

        public string SelectedDisplay
        {
            get => App.Settings.Prop.ClippingDisplayDevice;
            set { App.Settings.Prop.ClippingDisplayDevice = value ?? ""; SaveAndNotify(nameof(SelectedDisplay)); }
        }

        public bool SpeakerEnabled
        {
            get => App.Settings.Prop.ClippingSpeakerEnabled;
            set { App.Settings.Prop.ClippingSpeakerEnabled = value; SaveAndNotify(nameof(SpeakerEnabled)); }
        }

        public string SelectedSpeaker
        {
            get => App.Settings.Prop.ClippingSpeakerDevice;
            set { App.Settings.Prop.ClippingSpeakerDevice = value ?? ""; SaveAndNotify(nameof(SelectedSpeaker)); }
        }

        public int SpeakerVolume
        {
            get => App.Settings.Prop.ClippingSpeakerVolume;
            set { App.Settings.Prop.ClippingSpeakerVolume = Math.Clamp(value, 0, 100); SaveAndNotify(nameof(SpeakerVolume)); }
        }

        public bool MicrophoneEnabled
        {
            get => App.Settings.Prop.ClippingMicrophoneEnabled;
            set { App.Settings.Prop.ClippingMicrophoneEnabled = value; SaveAndNotify(nameof(MicrophoneEnabled)); }
        }

        public string SelectedMicrophone
        {
            get => App.Settings.Prop.ClippingMicrophoneDevice;
            set { App.Settings.Prop.ClippingMicrophoneDevice = value ?? ""; SaveAndNotify(nameof(SelectedMicrophone)); }
        }

        public int MicrophoneVolume
        {
            get => App.Settings.Prop.ClippingMicrophoneVolume;
            set { App.Settings.Prop.ClippingMicrophoneVolume = Math.Clamp(value, 0, 100); SaveAndNotify(nameof(MicrophoneVolume)); }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public Visibility EmptyVisibility => Clips.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        public string ClipCountText => Clips.Count == 1 ? "1 recording" : $"{Clips.Count} recordings";

        public ClippingViewModel()
        {
            Directory.CreateDirectory(Paths.Playbacks);
            (Displays, Speakers, Microphones) = LoadDevices();
            _statusText = ClippingEnabled ? "Ready — Alt + X is active system-wide" : "Disabled";
            RefreshClips();

            _clipWatcher = new FileSystemWatcher(Paths.Playbacks, "*.*")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = true,
                EnableRaisingEvents = false
            };
            _clipWatcher.Created += ClipsChanged;
            _clipWatcher.Changed += ClipsChanged;
            _clipWatcher.Deleted += ClipsChanged;
            _clipWatcher.Renamed += ClipsChanged;

            _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveRefreshTimer.Tick += (_, _) => RefreshClips();
        }

        public void StartLiveUpdates()
        {
            if (_disposed)
                return;

            _clipWatcher.EnableRaisingEvents = true;
            _liveRefreshTimer.Start();
            RefreshClips(true);
        }

        public void StopLiveUpdates()
        {
            if (_disposed)
                return;

            _liveRefreshTimer.Stop();
            _clipWatcher.EnableRaisingEvents = false;
        }

        public void SetHotkey(int modifiers, int virtualKey)
        {
            App.Settings.Prop.ClippingHotkeyModifiers = modifiers;
            App.Settings.Prop.ClippingHotkeyVirtualKey = virtualKey;
            App.Settings.Save();
            Services.ClippingHostService.Restart();
            OnPropertyChanged(nameof(HotkeyDisplay));
            StatusText = $"Save hotkey set to {HotkeyDisplay}";
        }

        public void BeginRename(ClipItem clip)
        {
            clip.EditName = clip.DisplayName;
            clip.IsRenaming = true;
        }

        public void CancelRename(ClipItem clip) => clip.IsRenaming = false;

        public void CommitRename(ClipItem clip)
        {
            string cleaned = String.Concat(clip.EditName.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            if (String.IsNullOrWhiteSpace(cleaned))
            {
                clip.IsRenaming = false;
                return;
            }

            string extension = Path.GetExtension(clip.FullPath);
            string destination = Path.Combine(Path.GetDirectoryName(clip.FullPath)!, cleaned + extension);
            if (!String.Equals(destination, clip.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(destination))
                    destination = Path.Combine(Path.GetDirectoryName(clip.FullPath)!, $"{cleaned} {DateTime.Now:HHmmss}{extension}");
                File.Move(clip.FullPath, destination);
            }
            RefreshClips();
        }

        public void DeleteClip(ClipItem clip)
        {
            File.Delete(clip.FullPath);
            RefreshClips();
        }

        public static void OpenClipLocation(ClipItem clip)
        {
            var startInfo = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                Arguments = $"/select,\"{clip.FullPath}\""
            };
            Process.Start(startInfo);
        }

        public static void OpenPlaybacksFolder()
        {
            Directory.CreateDirectory(Paths.Playbacks);
            Process.Start(new ProcessStartInfo(Paths.Playbacks) { UseShellExecute = true });
        }

        public void RefreshClips(bool force = false)
        {
            try
            {
                FileInfo[] files = Directory.EnumerateFiles(Paths.Playbacks, "*.*", SearchOption.AllDirectories)
                    .Where(path => VideoExtensions.Contains(Path.GetExtension(path)))
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToArray();

                string snapshot = String.Join("\n", files.Select(file => $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"));
                if (!force && snapshot == _clipSnapshot)
                    return;

                _clipSnapshot = snapshot;

                Clips.Clear();
                foreach (FileInfo file in files)
                {
                    try { Clips.Add(new ClipItem(file.FullName)); }
                    catch (IOException ex) { App.Logger.WriteException("ClippingViewModel::RefreshClips", ex); }
                    catch (UnauthorizedAccessException ex) { App.Logger.WriteException("ClippingViewModel::RefreshClips", ex); }
                }
                OnPropertyChanged(nameof(EmptyVisibility));
                OnPropertyChanged(nameof(ClipCountText));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ClippingViewModel::RefreshClips", ex);
            }
        }

        private void ClipsChanged(object sender, FileSystemEventArgs e)
        {
            if (!VideoExtensions.Contains(Path.GetExtension(e.FullPath)) || Interlocked.Exchange(ref _refreshPending, 1) == 1)
                return;

            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(250);
                try
                {
                    if (!_disposed)
                        RefreshClips();
                }
                finally { Interlocked.Exchange(ref _refreshPending, 0); }
            });
        }

        private static (IReadOnlyList<DeviceChoice>, IReadOnlyList<DeviceChoice>, IReadOnlyList<DeviceChoice>) LoadDevices()
        {
            var displays = new List<DeviceChoice> { new("Primary monitor", "") };
            var speakers = new List<DeviceChoice> { new("System default", "") };
            var microphones = new List<DeviceChoice> { new("System default", "") };

            try
            {
                int monitorNumber = 1;
                foreach (Forms.Screen screen in Forms.Screen.AllScreens)
                {
                    if (screen.Primary)
                        continue;

                    monitorNumber++;
                    displays.Add(new DeviceChoice(
                        $"Monitor {monitorNumber} ({screen.Bounds.Width}×{screen.Bounds.Height})",
                        screen.DeviceName));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ClippingViewModel::LoadDevices", ex);
            }

            return (displays, speakers, microphones);
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

        private void SaveAndNotify(string property)
        {
            App.Settings.Save();
            OnPropertyChanged(property);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _liveRefreshTimer.Stop();
            _clipWatcher.EnableRaisingEvents = false;
            _clipWatcher.Dispose();
        }
    }
}
