using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

using SleepStrap.UI.ViewModels.Settings;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using SleepStrap.UI.Elements.Settings.Pages;

namespace SleepStrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;

            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            LoadState();

            string? lastPageName = App.State.Prop.LastPage;
            Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

            if (lastPage == typeof(FontsPage))
                lastPage = typeof(TexturesPage);

            Type[] visiblePages = { typeof(SkyboxPage), typeof(TexturesPage), typeof(RivalsPage), typeof(MacroPage), typeof(ClippingPage), typeof(OtherPage) };
            if (lastPage != null && visiblePages.Contains(lastPage))
                SafeNavigate(lastPage);

            RootNavigation.Navigated += OnNavigation!;
            RootFrame.Navigated += AnimatePageNavigation;

            void OnNavigation(object? sender, RoutedNavigationEventArgs e)
            {
                INavigationItem? currentPage = RootNavigation.Current;

                App.State.Prop.LastPage = currentPage?.PageType.FullName!;
            }
        }

        private async void SettingsIntroOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 };
            SettingsIntroLogo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            if (SettingsIntroLogo.RenderTransform is ScaleTransform logoScale)
            {
                logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(580)) { EasingFunction = pop });
                logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.5, 1, TimeSpan.FromMilliseconds(580)) { EasingFunction = pop });
            }

            SettingsIntroGlow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.12, 0.34, TimeSpan.FromMilliseconds(820))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

            int index = 0;
            foreach (TextBlock letter in SettingsBrandLetters.Children.OfType<TextBlock>())
            {
                letter.FontSize = 38;
                letter.FontWeight = FontWeights.SemiBold;
                letter.Foreground = Brushes.White;
                letter.Opacity = 0;
                letter.RenderTransformOrigin = new Point(0.5, 0.72);
                var scale = new ScaleTransform(0.58, 0.58);
                var rise = new TranslateTransform(0, 24);
                var group = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(rise);
                letter.RenderTransform = group;

                TimeSpan delay = TimeSpan.FromMilliseconds(150 + index * 68);
                letter.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { BeginTime = delay });
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.58, 1, TimeSpan.FromMilliseconds(410)) { BeginTime = delay, EasingFunction = pop });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.58, 1, TimeSpan.FromMilliseconds(410)) { BeginTime = delay, EasingFunction = pop });
                rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(410)) { BeginTime = delay, EasingFunction = pop });
                index++;
            }

            if (SettingsBrandShine.RenderTransform is TranslateTransform shine)
            {
                shine.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-80, 330, TimeSpan.FromMilliseconds(700))
                {
                    BeginTime = TimeSpan.FromMilliseconds(940),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
                SettingsBrandShine.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = TimeSpan.FromMilliseconds(940),
                    Duration = TimeSpan.FromMilliseconds(700),
                    KeyFrames =
                    {
                        new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)),
                        new LinearDoubleKeyFrame(0.8, KeyTime.FromPercent(0.18)),
                        new LinearDoubleKeyFrame(0.8, KeyTime.FromPercent(0.72)),
                        new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1))
                    }
                });
            }

            await Task.Delay(1700);
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            fade.Completed += (_, _) => SettingsIntroOverlay.Visibility = Visibility.Collapsed;
            SettingsIntroOverlay.BeginAnimation(OpacityProperty, fade);
        }

        private static void AnimatePageNavigation(object sender, NavigationEventArgs e)
        {
            if (e.Content is not FrameworkElement page)
                return;

            page.Opacity = 0;
            var offset = new TranslateTransform(0, 12);
            page.RenderTransform = offset;

            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = easing });
            offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(330)) { EasingFunction = easing });
        }

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void SafeNavigate(Type page)
        {
            await Task.Delay(500); // same as below

            Navigate(page);
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500); // wait for everything to finish loading
            AlreadyRunningSnackbar.Show();
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }

            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }
    }
}
