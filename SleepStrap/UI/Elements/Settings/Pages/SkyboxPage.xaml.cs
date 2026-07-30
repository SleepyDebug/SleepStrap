using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SleepStrap.Models;
using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class SkyboxPage
    {
        private readonly DispatcherTimer _favoriteTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private SkyboxChoice? _pressedChoice;
        private bool _favoriteTriggered;

        public SkyboxPage()
        {
            DataContext = new SkyboxViewModel();
            InitializeComponent();
            if (DataContext is SkyboxViewModel viewModel)
                viewModel.SkyboxChoices.CollectionChanged += SkyboxChoices_CollectionChanged;
            _favoriteTimer.Tick += (_, _) =>
            {
                _favoriteTimer.Stop();
                _favoriteTriggered = true;
                (DataContext as SkyboxViewModel)?.ToggleFavorite(_pressedChoice);
            };
        }

        private void SkyboxChoices_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(AnimateCards));
        }

        private void AnimateCards()
        {
            foreach (object item in SkyboxList.Items)
            {
                if (SkyboxList.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container)
                    continue;

                var offset = new TranslateTransform(0, -10);
                container.RenderTransform = offset;
                container.Opacity = 0.35;
                var ease = new SineEase { EasingMode = EasingMode.EaseOut };
                container.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
                offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease });
            }
        }

        private void SkyCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null)
                return;
            _pressedChoice = (sender as FrameworkElement)?.DataContext as SkyboxChoice;
            _favoriteTriggered = false;
            if (sender is IInputElement input)
                Mouse.Capture(input);
            _favoriteTimer.Start();
            // Selection is committed on mouse-up. This keeps a long press from
            // selecting a card before it is pinned.
            e.Handled = true;
        }

        private void SkyCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _favoriteTimer.Stop();
            Mouse.Capture(null);
            if (!_favoriteTriggered && _pressedChoice is not null)
                SkyboxList.SelectedItem = _pressedChoice;
            e.Handled = true;
            _pressedChoice = null;
        }

        private void FavoriteStar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SkyboxChoice choice)
            {
                (DataContext as SkyboxViewModel)?.ToggleFavorite(choice);
                e.Handled = true;
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child is not null)
            {
                if (child is T match) return match;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
