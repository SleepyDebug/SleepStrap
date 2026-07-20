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

            Type[] visiblePages = { typeof(SkyboxPage), typeof(TexturesPage), typeof(RivalsPage), typeof(FontsPage), typeof(OtherPage) };
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
