using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Interop;

using ScreenRecorderLib;

using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace SleepStrap.Services
{
    internal sealed class ReplayBufferService : IDisposable
    {
        private const int SegmentSeconds = 20;
        private const int HotkeyId = 0x534C;

        private readonly CancellationTokenSource _stopToken = new();
        private readonly SemaphoreSlim _rotateSignal = new(0, 1);
        private readonly List<ReplaySegment> _segments = new();
        private readonly object _segmentLock = new();

        private HwndSource? _hotkeyWindow;
        private Recorder? _recorder;
        private Task? _captureTask;
        private int _saveRequested;
        private bool _disposed;

        public void Start()
        {
            if (_captureTask is not null || !App.Settings.Prop.ClippingEnabled)
                return;

            Directory.CreateDirectory(Paths.Playbacks);
            Directory.CreateDirectory(GetBufferDirectory());
            RegisterHotkey();
            App.Logger.WriteLine("ReplayBufferService::Start", "Starting the replay capture loop");
            _captureTask = Task.Run(() => CaptureLoopAsync(_stopToken.Token));
        }

        public void SaveReplay()
        {
            if (_captureTask is null)
                return;

            Interlocked.Exchange(ref _saveRequested, 1);
            try { _rotateSignal.Release(); } catch (SemaphoreFullException) { }
            App.Logger.WriteLine("ReplayBufferService::SaveReplay", "Replay save requested");
        }

        private async Task CaptureLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string segmentPath = Path.Combine(GetBufferDirectory(), $"segment-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.mp4");
                DateTime started = DateTime.UtcNow;
                var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                try
                {
                    Recorder recorder = CreateRecorder();
                    _recorder = recorder;
                    recorder.OnRecordingComplete += (_, e) => completion.TrySetResult(e.FilePath ?? segmentPath);
                    recorder.OnRecordingFailed += (_, e) => completion.TrySetException(new InvalidOperationException(e.Error));
                    recorder.Record(segmentPath);

                    using (var cycleToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        Task timer = Task.Delay(TimeSpan.FromSeconds(SegmentSeconds), cycleToken.Token);
                        Task signal = _rotateSignal.WaitAsync(cycleToken.Token);
                        await Task.WhenAny(timer, signal);
                        cycleToken.Cancel();
                    }

                    recorder.Stop();
                    Task completedOrTimeout = await Task.WhenAny(completion.Task, Task.Delay(5000, cancellationToken));
                    string completedPath = completedOrTimeout == completion.Task && completion.Task.IsCompletedSuccessfully
                        ? completion.Task.Result
                        : segmentPath;
                    recorder.Dispose();
                    _recorder = null;

                    await WaitForFileReadyAsync(completedPath, cancellationToken);

                    if (File.Exists(completedPath) && new FileInfo(completedPath).Length > 0)
                    {
                        lock (_segmentLock)
                            _segments.Add(new ReplaySegment(completedPath, started, DateTime.UtcNow));
                    }

                    PruneOldSegments();

                    if (Interlocked.Exchange(ref _saveRequested, 0) == 1)
                    {
                        List<ReplaySegment> snapshot;
                        lock (_segmentLock)
                            snapshot = _segments.ToList();

                        _ = Task.Run(() => RenderReplayAsync(snapshot));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("ReplayBufferService::Capture", ex);
                    try { _recorder?.Dispose(); } catch { }
                    _recorder = null;
                    TryDelete(segmentPath);
                    try { await Task.Delay(2000, cancellationToken); } catch (OperationCanceledException) { break; }
                }
            }
        }

        private static Recorder CreateRecorder()
        {
            string display = App.Settings.Prop.ClippingDisplayDevice;
            if (String.IsNullOrWhiteSpace(display))
                display = System.Windows.Forms.Screen.PrimaryScreen?.DeviceName ?? @"\\.\DISPLAY1";
            var source = new DisplayRecordingSource(display);
            // ScreenRecorderLib 6.6 can access-violate when Windows Graphics Capture
            // is initialized from the background watcher. Desktop Duplication is the
            // stable display backend here; software encoding below keeps it isolated
            // from Roblox's hardware encoder resources.
            source.RecorderApi = RecorderApi.DesktopDuplication;
            source.IsCursorCaptureEnabled = true;
            source.IsBorderRequired = false;
            ScreenRecorderLib.ScreenSize outputSize = GetCaptureSize(display);
            int framerate = outputSize.Width > 2560 ? 30 : 60;

            bool speaker = App.Settings.Prop.ClippingSpeakerEnabled;
            bool microphone = App.Settings.Prop.ClippingMicrophoneEnabled;

            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = new List<RecordingSourceBase> { source }
                },
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    OutputFrameSize = outputSize,
                    Stretch = StretchMode.Uniform
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = speaker || microphone,
                    IsOutputDeviceEnabled = speaker,
                    IsInputDeviceEnabled = microphone,
                    AudioOutputDevice = NullIfDefault(App.Settings.Prop.ClippingSpeakerDevice),
                    AudioInputDevice = NullIfDefault(App.Settings.Prop.ClippingMicrophoneDevice),
                    OutputVolume = App.Settings.Prop.ClippingSpeakerVolume / 100f,
                    InputVolume = App.Settings.Prop.ClippingMicrophoneVolume / 100f,
                    Channels = AudioChannels.Stereo,
                    Bitrate = AudioBitrate.bitrate_192kbps
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.Quality,
                        EncoderProfile = H264Profile.High
                    },
                    Framerate = framerate,
                    Bitrate = 45_000_000,
                    Quality = 95,
                    IsFixedFramerate = true,
                    // Keep the encoder off Roblox's GPU context. Quality remains high,
                    // but capture can no longer force a hardware encoder reset in-game.
                    IsHardwareEncodingEnabled = false,
                    IsLowLatencyEnabled = false,
                    IsFragmentedMp4Enabled = false,
                    IsMp4FastStartEnabled = false
                },
                MouseOptions = new MouseOptions
                {
                    IsMousePointerEnabled = true,
                    IsMouseClicksDetected = false
                },
                LogOptions = new LogOptions
                {
                    IsLogEnabled = true,
                    LogFilePath = Path.Combine(Paths.Logs, "ReplayBuffer.log"),
                    LogSeverityLevel = ScreenRecorderLib.LogLevel.Warn
                }
            };

            return Recorder.CreateRecorder(options);
        }

        private async Task RenderReplayAsync(IReadOnlyList<ReplaySegment> availableSegments)
        {
            try
            {
                Directory.CreateDirectory(Paths.Playbacks);
                TimeSpan wanted = TimeSpan.FromMinutes(Math.Clamp(App.Settings.Prop.ClippingBufferMinutes, 1, 10));
                DateTime cutoff = DateTime.UtcNow - wanted;
                List<ReplaySegment> selected = availableSegments
                    .Where(x => x.EndedUtc >= cutoff && File.Exists(x.Path))
                    .OrderBy(x => x.StartedUtc)
                    .ToList();

                if (selected.Count == 0)
                {
                    App.Logger.WriteLine("ReplayBufferService::SaveReplay", "Replay buffer is still warming up");
                    return;
                }

                var composition = new MediaComposition();
                foreach (ReplaySegment segment in selected)
                {
                    StorageFile file = await StorageFile.GetFileFromPathAsync(segment.Path);
                    MediaClip clip = await MediaClip.CreateFromFileAsync(file);
                    composition.Clips.Add(clip);
                }

                TimeSpan duration = composition.Duration;
                if (duration > wanted && composition.Clips.Count > 0)
                    composition.Clips[0].TrimTimeFromStart = duration - wanted;

                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(Paths.Playbacks);
                StorageFile output = await folder.CreateFileAsync(
                    $"Playback {DateTime.Now:yyyy-MM-dd HH-mm-ss}.mp4",
                    CreationCollisionOption.GenerateUniqueName);

                VideoEncodingProperties sourceProperties = composition.Clips[0].GetVideoEncodingProperties();
                VideoEncodingQuality quality = sourceProperties.Width > 1920 || sourceProperties.Height > 1080
                    ? VideoEncodingQuality.Uhd2160p
                    : VideoEncodingQuality.HD1080p;
                MediaEncodingProfile profile = MediaEncodingProfile.CreateMp4(quality);
                profile.Video.Width = sourceProperties.Width;
                profile.Video.Height = sourceProperties.Height;
                profile.Video.FrameRate.Numerator = sourceProperties.FrameRate.Numerator;
                profile.Video.FrameRate.Denominator = sourceProperties.FrameRate.Denominator;
                TranscodeFailureReason result = await composition.RenderToFileAsync(
                    output,
                    MediaTrimmingPreference.Precise,
                    profile);

                if (result != TranscodeFailureReason.None)
                    throw new InvalidOperationException($"Windows could not render the replay ({result}).");

                App.Logger.WriteLine("ReplayBufferService::SaveReplay", $"Saved {output.Path}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ReplayBufferService::SaveReplay", ex);
            }
        }

        private void PruneOldSegments()
        {
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-Math.Clamp(App.Settings.Prop.ClippingBufferMinutes, 1, 10)).AddSeconds(-SegmentSeconds * 2);
            List<ReplaySegment> expired;
            lock (_segmentLock)
            {
                expired = _segments.Where(x => x.EndedUtc < cutoff).ToList();
                _segments.RemoveAll(x => x.EndedUtc < cutoff);
            }

            foreach (ReplaySegment segment in expired)
                TryDelete(segment.Path);
        }

        private void RegisterHotkey()
        {
            _hotkeyWindow = new HwndSource(new HwndSourceParameters("SleepStrap Replay Hotkey")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0
            });
            _hotkeyWindow.AddHook(HotkeyWindowHook);

            uint modifiers = (uint)App.Settings.Prop.ClippingHotkeyModifiers | 0x4000; // MOD_NOREPEAT
            uint virtualKey = (uint)App.Settings.Prop.ClippingHotkeyVirtualKey;
            if (!RegisterHotKey(_hotkeyWindow.Handle, HotkeyId, modifiers, virtualKey))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, "The configured replay hotkey could not be registered");
            }

            App.Logger.WriteLine("ReplayBufferService::Hotkey", $"Registered global replay hotkey modifiers=0x{modifiers:X} key=0x{virtualKey:X}");
        }

        private IntPtr HotkeyWindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0312 && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                App.Logger.WriteLine("ReplayBufferService::Hotkey", "Global replay hotkey pressed");
                SaveReplay();
            }

            return IntPtr.Zero;
        }

        private static string GetBufferDirectory() => Path.Combine(Paths.Temp, "ReplayBuffer", Environment.ProcessId.ToString());
        private static ScreenRecorderLib.ScreenSize GetCaptureSize(string deviceName)
        {
            System.Windows.Forms.Screen? screen = System.Windows.Forms.Screen.AllScreens
                .FirstOrDefault(x => String.Equals(x.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            double width = screen?.Bounds.Width ?? 1920;
            double height = screen?.Bounds.Height ?? 1080;
            if (width > 3840)
            {
                height = Math.Round(height * (3840 / width));
                width = 3840;
            }
            width -= width % 2;
            height -= height % 2;
            return new ScreenRecorderLib.ScreenSize(width, height);
        }
        private static string? NullIfDefault(string value) => String.IsNullOrWhiteSpace(value) ? null : value;
        private static void TryDelete(string path) { try { File.Delete(path); } catch { } }

        private static async Task WaitForFileReadyAsync(string path, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 0)
                    {
                        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return;
                    }
                }
                catch (IOException) { }

                await Task.Delay(100, cancellationToken);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _stopToken.Cancel();
            try { _recorder?.Stop(); } catch { }
            try { _recorder?.Dispose(); } catch { }
            _recorder = null;
            try { _captureTask?.Wait(TimeSpan.FromSeconds(3)); } catch { }

            if (_hotkeyWindow is not null)
            {
                HwndSource window = _hotkeyWindow;
                window.Dispatcher.Invoke(() =>
                {
                    UnregisterHotKey(window.Handle, HotkeyId);
                    window.RemoveHook(HotkeyWindowHook);
                    window.Dispose();
                });
                _hotkeyWindow = null;
            }

            lock (_segmentLock)
            {
                foreach (ReplaySegment segment in _segments)
                    TryDelete(segment.Path);
                _segments.Clear();
            }

            try { Directory.Delete(GetBufferDirectory(), true); } catch { }
            _rotateSignal.Dispose();
            _stopToken.Dispose();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private sealed record ReplaySegment(string Path, DateTime StartedUtc, DateTime EndedUtc);
    }
}
