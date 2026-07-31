using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SleepStrap.Services
{
    /// <summary>
    /// Reads only the lower-right portion of the focused Roblox client in memory
    /// and caches whether the Hand Gun HUD label is visible. No capture is saved
    /// or sent anywhere. The result is deliberately fail-closed: an unavailable
    /// OCR language or capture error leaves hold-to-spam off.
    /// </summary>
    internal sealed class RobloxHandgunHudDetector : IDisposable
    {
        private const int ScanIntervalMilliseconds = 200;
        private const int ResultFreshnessMilliseconds = 650;
        private const double CaptureWidthFraction = 0.45;
        private const double CaptureHeightFraction = 0.35;

        private readonly object _engineLock = new();
        private OcrEngine? _engine;
        private bool _engineWasCreated;
        private int _scanInProgress;
        private int _handgunVisible;
        private long _lastScanUtcTicks;
        private bool _ocrUnavailableLogged;
        private bool _scanFailureLogged;
        private bool _disposed;

        public bool IsHandgunVisible
        {
            get
            {
                if (Volatile.Read(ref _handgunVisible) == 0)
                    return false;

                long lastScan = Interlocked.Read(ref _lastScanUtcTicks);
                return lastScan != 0 &&
                    DateTime.UtcNow.Ticks - lastScan <= TimeSpan.FromMilliseconds(ResultFreshnessMilliseconds).Ticks;
            }
        }

        public async Task RefreshIfDueAsync(IntPtr robloxWindow, CancellationToken cancellationToken)
        {
            if (_disposed || robloxWindow == IntPtr.Zero)
            {
                Clear();
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            long lastScan = Interlocked.Read(ref _lastScanUtcTicks);
            if (lastScan != 0 && now - lastScan < TimeSpan.FromMilliseconds(ScanIntervalMilliseconds).Ticks)
                return;

            if (Interlocked.CompareExchange(ref _scanInProgress, 1, 0) != 0)
                return;

            try
            {
                // Mark the interval before OCR starts so a slow OCR pass cannot
                // schedule duplicate captures from the click loop.
                Interlocked.Exchange(ref _lastScanUtcTicks, now);
                bool isHandgun = await IsHandgunLabelVisibleAsync(robloxWindow, cancellationToken);
                Volatile.Write(ref _handgunVisible, isHandgun ? 1 : 0);
            }
            catch (OperationCanceledException)
            {
                Clear();
            }
            catch (Exception ex)
            {
                Clear();
                if (!_scanFailureLogged)
                {
                    _scanFailureLogged = true;
                    App.Logger.WriteException("RobloxHandgunHudDetector::Refresh", ex);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _scanInProgress, 0);
            }
        }

        public void Clear()
        {
            Volatile.Write(ref _handgunVisible, 0);
            Interlocked.Exchange(ref _lastScanUtcTicks, 0);
        }

        private async Task<bool> IsHandgunLabelVisibleAsync(IntPtr robloxWindow, CancellationToken cancellationToken)
        {
            OcrEngine? engine = GetOcrEngine();
            if (engine is null)
                return false;

            cancellationToken.ThrowIfCancellationRequested();
            using Bitmap capture = CaptureLowerRightHud(robloxWindow);
            byte[] imageBytes;
            using (var imageStream = new MemoryStream())
            {
                capture.Save(imageStream, ImageFormat.Png);
                imageBytes = imageStream.ToArray();
            }

            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore);
            OcrResult result = await engine.RecognizeAsync(bitmap);

            // Roblox can render this as either "Hand Gun" or "Handgun". OCR
            // output is normalized so spacing, capitalization, and outlines do
            // not make the gate miss the same HUD label.
            string normalized = Regex.Replace(result.Text ?? String.Empty, "[^A-Za-z]", String.Empty);
            return normalized.Contains("HANDGUN", StringComparison.OrdinalIgnoreCase);
        }

        private OcrEngine? GetOcrEngine()
        {
            lock (_engineLock)
            {
                if (_engineWasCreated)
                    return _engine;

                _engineWasCreated = true;
                _engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                    ?? OcrEngine.TryCreateFromUserProfileLanguages();

                if (_engine is null && !_ocrUnavailableLogged)
                {
                    _ocrUnavailableLogged = true;
                    App.Logger.WriteLine(
                        "RobloxHandgunHudDetector::OCR",
                        "Windows OCR is unavailable, so Hand Gun-only hold-to-spam will remain off.");
                }

                return _engine;
            }
        }

        private static Bitmap CaptureLowerRightHud(IntPtr robloxWindow)
        {
            if (!GetClientRect(robloxWindow, out RECT clientRect))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the Roblox client area.");

            int clientWidth = clientRect.Right - clientRect.Left;
            int clientHeight = clientRect.Bottom - clientRect.Top;
            if (clientWidth < 1 || clientHeight < 1)
                throw new InvalidOperationException("The Roblox window has no usable client area.");

            POINT origin = new() { X = 0, Y = 0 };
            if (!ClientToScreen(robloxWindow, ref origin))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not locate the Roblox client area.");

            int captureWidth = Math.Clamp((int)Math.Ceiling(clientWidth * CaptureWidthFraction), 1, clientWidth);
            int captureHeight = Math.Clamp((int)Math.Ceiling(clientHeight * CaptureHeightFraction), 1, clientHeight);
            int captureLeft = origin.X + clientWidth - captureWidth;
            int captureTop = origin.Y + clientHeight - captureHeight;

            var screenshot = new Bitmap(captureWidth, captureHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(
                    captureLeft,
                    captureTop,
                    0,
                    0,
                    new Size(captureWidth, captureHeight),
                    CopyPixelOperation.SourceCopy);
            }

            return screenshot;
        }

        public void Dispose()
        {
            _disposed = true;
            Clear();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr window, ref POINT point);
    }
}
