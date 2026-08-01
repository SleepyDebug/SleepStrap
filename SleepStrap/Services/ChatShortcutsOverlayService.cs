using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SleepStrap.Services
{
    internal sealed class ChatShortcutsOverlayService : IDisposable
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(700) };
        private readonly List<ChatShortcutOverlayWindow> _overlays = new();
        private OcrEngine? _ocr;
        private bool _scanning;

        public void Start()
        {
            _ocr = OcrEngine.TryCreateFromLanguage(new Language("en-US")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
            if (_ocr is null) { App.Logger.WriteLine("ChatShortcuts", "Windows OCR unavailable."); return; }
            _timer.Tick += async (_, _) => await ScanAsync();
            _timer.Start();
        }

        private async Task ScanAsync()
        {
            if (_scanning || _ocr is null) return;
            IntPtr window = FindRobloxWindow();
            if (window == IntPtr.Zero) { HideFrom(0); return; }
            _scanning = true;
            try
            {
                using Bitmap capture = CaptureChat(window, out Rectangle chatArea);
                IReadOnlyList<Callout> callouts = await ReadCalloutsAsync(capture);
                Show(callouts.Select(callout => new ScreenCallout(callout.Text, ToScreenArea(chatArea, callout))).ToArray());
            }
            catch (Exception ex) when (ex is Win32Exception or ExternalException or InvalidOperationException) { App.Logger.WriteException("ChatShortcuts::Scan", ex); }
            finally { _scanning = false; }
        }

        private async Task<IReadOnlyList<Callout>> ReadCalloutsAsync(Bitmap capture)
        {
            byte[] bytes;
            using (var scaled = new Bitmap(capture.Width * 2, capture.Height * 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(scaled))
            using (var memory = new MemoryStream())
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(capture, 0, 0, scaled.Width, scaled.Height);
                scaled.Save(memory, ImageFormat.Png); bytes = memory.ToArray();
            }
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream)) { writer.WriteBytes(bytes); await writer.StoreAsync(); writer.DetachStream(); }
            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            OcrResult result = await _ocr!.RecognizeAsync(bitmap);
            var callouts = new List<Callout>();
            foreach (OcrLine line in result.Lines)
            foreach (OcrWord word in line.Words)
            {
                string code = Regex.Replace(word.Text ?? "", "[^A-Za-z]", "").ToUpperInvariant();
                string? text = code switch { "H" => "HIGH HP", "M" => "MEDIUM HP", "L" => "LOW HP", "T" => "TOGETHER", "S" => "SPLIT", _ => null };
                if (text is not null) callouts.Add(new Callout(text, word.BoundingRect.X, word.BoundingRect.Y, word.BoundingRect.Width, word.BoundingRect.Height));
            }
            return callouts;
        }

        private void Show(IReadOnlyList<ScreenCallout> callouts)
        {
            while (_overlays.Count < callouts.Count) _overlays.Add(new ChatShortcutOverlayWindow());
            for (int i = 0; i < callouts.Count; i++) _overlays[i].ShowCallout(callouts[i].Area, callouts[i].Text);
            HideFrom(callouts.Count);
        }
        private void HideFrom(int index) { for (int i = index; i < _overlays.Count; i++) _overlays[i].Hide(); }
        private static Rectangle ToScreenArea(Rectangle chat, Callout callout)
        {
            int textWidth = callout.Text switch { "MEDIUM HP" => 112, "TOGETHER" => 96, "HIGH HP" => 76, "SPLIT" => 62, _ => 66 };
            return new Rectangle(chat.Left + Math.Max(0, (int)(callout.X / 2) - 1), chat.Top + Math.Max(0, (int)(callout.Y / 2) - 1), Math.Max(textWidth, (int)(callout.Width / 2) + 12), Math.Max(18, (int)(callout.Height / 2) + 2));
        }

        private static Bitmap CaptureChat(IntPtr window, out Rectangle area)
        {
            if (!GetClientRect(window, out RECT client)) throw new Win32Exception(Marshal.GetLastWin32Error());
            POINT origin = new(); if (!ClientToScreen(window, ref origin)) throw new Win32Exception(Marshal.GetLastWin32Error());
            int width = client.Right - client.Left, height = client.Bottom - client.Top;
            if (width < 200 || height < 200) throw new InvalidOperationException("Roblox too small.");
            area = new Rectangle(origin.X, origin.Y, Math.Min((int)(width * .40), 620), Math.Min((int)(height * .52), 460));
            var image = new Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(image);
            graphics.CopyFromScreen(area.Left, area.Top, 0, 0, image.Size, CopyPixelOperation.SourceCopy);
            return image;
        }

        private static IntPtr FindRobloxWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window)) return true;
                GetWindowThreadProcessId(window, out uint id);
                try { if (System.Diagnostics.Process.GetProcessById((int)id).ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase)) { found = window; return false; } } catch { }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public void Dispose() { _timer.Stop(); foreach (ChatShortcutOverlayWindow overlay in _overlays) overlay.Close(); }
        private readonly record struct Callout(string Text, double X, double Y, double Width, double Height);
        private readonly record struct ScreenCallout(string Text, Rectangle Area);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetClientRect(IntPtr window, out RECT rectangle);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool ClientToScreen(IntPtr window, ref POINT point);
    }

    internal sealed class ChatShortcutOverlayWindow : Window
    {
        private readonly TextBlock _label;
        public ChatShortcutOverlayWindow()
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent; Topmost = true; ShowInTaskbar = false; IsHitTestVisible = false;
            _label = new TextBlock { Foreground = System.Windows.Media.Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 2, 0) };
            Content = new Border { Background = System.Windows.Media.Brushes.Black, Opacity = .96, Child = _label };
            SourceInitialized += (_, _) => { IntPtr handle = new WindowInteropHelper(this).Handle; SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x20 | 0x80); SetWindowDisplayAffinity(handle, 0x00000011); };
        }
        public void ShowCallout(Rectangle area, string text) { _label.Text = text; Left = area.Left; Top = area.Top; Width = area.Width; Height = area.Height; if (!IsVisible) Show(); }
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr handle, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr handle, int index, int value);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr handle, uint affinity);
    }
}
