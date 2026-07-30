using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace SleepStrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        private readonly IThemeService _themeService = new ThemeService();

        public WpfUiWindow()
        {
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            const int customThemeIndex = 2; // index for CustomTheme merged dictionary

            _themeService.SetTheme(App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Dark ? ThemeType.Dark : ThemeType.Light);
            _themeService.SetAccent(System.Windows.Media.Color.FromRgb(255, 255, 255));

            // SleepStrap's original UI is monochrome. WPF UI otherwise restores
            // the Windows blue accent when a window theme is applied.
            Application.Current.Resources["SystemAccentColor"] = Colors.White;
            Application.Current.Resources["SystemAccentColorPrimary"] = Color.FromRgb(242, 242, 244);
            Application.Current.Resources["SystemAccentColorSecondary"] = Color.FromRgb(220, 220, 224);
            Application.Current.Resources["SystemAccentColorTertiary"] = Color.FromRgb(190, 190, 196);
            Application.Current.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(Color.FromRgb(220, 220, 224));
            Application.Current.Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(220, 220, 224));
            Application.Current.Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(Color.FromRgb(190, 190, 196));

            // there doesn't seem to be a way to query the name for merged dictionaries
            var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Style/{Enum.GetName(App.Settings.Prop.Theme.GetFinal())}.xaml") };
            Application.Current.Resources.MergedDictionaries[customThemeIndex] = dict;

#if QA_BUILD
            this.BorderBrush = System.Windows.Media.Brushes.Black;
            this.BorderThickness = new Thickness(4);
#endif
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
        }
    }
}
