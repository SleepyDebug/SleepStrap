using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class TexturesPage
    {
        public TexturesPage()
        {
            DataContext = new VisualModsViewModel();
            InitializeComponent();
            Loaded += (_, _) => { BuildFontColorWheel(); UpdateFontColorUi(); };
        }

        private void FontColorButton_Click(object sender, RoutedEventArgs e)
        {
            BuildFontColorWheel();
            UpdateFontColorUi();
            FontColorPopup.IsOpen = true;
        }

        private void FontColorWheel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(FontColorWheel);
            double center = FontColorWheel.Width / 2;
            double dx = point.X - center, dy = point.Y - center;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            if (radius > center || DataContext is not VisualModsViewModel viewModel)
                return;
            double hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 450) % 360;
            Color color = HsvToColor(hue, Math.Clamp(radius / center, 0, 1), 1);
            viewModel.FontColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            UpdateFontColorUi();
        }

        private void UpdateFontColorUi()
        {
            if (DataContext is not VisualModsViewModel viewModel || FontColorValue is null)
                return;
            FontColorValue.Text = viewModel.FontColor;
            if (ColorConverter.ConvertFromString(viewModel.FontColor) is Color color)
                FontColorSwatch.Background = new SolidColorBrush(color);
        }

        private void BuildFontColorWheel()
        {
            const int size = 220;
            var pixels = new byte[size * size * 4];
            double center = size / 2d;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double dx = x - center, dy = y - center;
                double radius = Math.Sqrt(dx * dx + dy * dy) / center;
                int index = (y * size + x) * 4;
                if (radius > 1) { pixels[index + 3] = 0; continue; }
                Color color = HsvToColor((Math.Atan2(dy, dx) * 180 / Math.PI + 450) % 360, radius, 1);
                pixels[index] = color.B; pixels[index + 1] = color.G; pixels[index + 2] = color.R; pixels[index + 3] = 255;
            }
            var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            FontColorWheel.Source = bitmap;
        }

        private static Color HsvToColor(double hue, double saturation, double value)
        {
            double c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
            (double r, double g, double b) = hue switch
            { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}
