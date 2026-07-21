using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class ClippingPage
    {
        private readonly ClippingViewModel _viewModel;
        private bool _capturingHotkey;
        private int _pendingModifiers;
        private int _pendingVirtualKey;

        public ClippingPage()
        {
            _viewModel = new ClippingViewModel();
            DataContext = _viewModel;
            InitializeComponent();
            Loaded += (_, _) => _viewModel.StartLiveUpdates();
            Unloaded += (_, _) => _viewModel.StopLiveUpdates();
        }

        private void HotkeyBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _capturingHotkey = true;
            _pendingModifiers = 0;
            _pendingVirtualKey = 0;
            HotkeyBox.Text = "Press keys, then Enter";
            HotkeyBox.Focus();
            Keyboard.Focus(HotkeyBox);
            e.Handled = true;
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingHotkey)
                return;

            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Enter)
            {
                if (_pendingVirtualKey != 0)
                    _viewModel.SetHotkey(_pendingModifiers, _pendingVirtualKey);
                FinishHotkeyCapture();
                return;
            }
            if (key == Key.Escape)
            {
                FinishHotkeyCapture();
                return;
            }
            if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            ModifierKeys modifiers = Keyboard.Modifiers;
            _pendingModifiers = 0;
            if (modifiers.HasFlag(ModifierKeys.Alt)) _pendingModifiers |= 1;
            if (modifiers.HasFlag(ModifierKeys.Control)) _pendingModifiers |= 2;
            if (modifiers.HasFlag(ModifierKeys.Shift)) _pendingModifiers |= 4;
            if (modifiers.HasFlag(ModifierKeys.Windows)) _pendingModifiers |= 8;
            _pendingVirtualKey = KeyInterop.VirtualKeyFromKey(key);
            HotkeyBox.Text = FormatPendingHotkey(key, modifiers) + "  •  press Enter";
        }

        private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_capturingHotkey)
                FinishHotkeyCapture();
        }

        private void FinishHotkeyCapture()
        {
            _capturingHotkey = false;
            HotkeyBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }

        private static string FormatPendingHotkey(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(key.ToString());
            return String.Join(" + ", parts);
        }

        private static MediaElement? FindMedia(DependencyObject source) => FindChild<MediaElement>(source, "PreviewMedia");

        private void ClipCard_MouseEnter(object sender, MouseEventArgs e)
        {
            MediaElement? media = FindMedia((DependencyObject)sender);
            if (media is null) return;
            if (((FrameworkElement)sender).DataContext is not ClippingViewModel.ClipItem clip)
                return;

            media.Tag = true;
            if (media.Source is null)
                media.Source = clip.MediaUri;
            media.Position = TimeSpan.Zero;
            media.Play();
        }

        private void ClipCard_MouseLeave(object sender, MouseEventArgs e)
        {
            MediaElement? media = FindMedia((DependencyObject)sender);
            if (media is null) return;
            ReleasePreview(media);
        }

        private void PreviewMedia_MediaOpened(object sender, RoutedEventArgs e)
        {
            var media = (MediaElement)sender;
            if (media.Tag is true)
            {
                media.Position = TimeSpan.Zero;
                media.Play();
            }
            else
            {
                ReleasePreview(media);
            }
        }

        private void PreviewMedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            var media = (MediaElement)sender;
            media.Position = TimeSpan.Zero;
            if (media.Tag is true)
                media.Play();
            else
                ReleasePreview(media);
        }

        private void PreviewMedia_Unloaded(object sender, RoutedEventArgs e) => ReleasePreview((MediaElement)sender);

        private static void ReleasePreview(MediaElement media)
        {
            media.Tag = false;
            try
            {
                media.Stop();
                media.Close();
                media.Source = null;
            }
            catch (InvalidOperationException)
            {
                // Media Foundation can already be shutting down during navigation.
            }
        }

        private void DeleteClip_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not ClippingViewModel.ClipItem clip)
                return;
            if (Frontend.ShowMessageBox($"Delete '{clip.DisplayName}'?", MessageBoxImage.Warning, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                _viewModel.DeleteClip(clip);
        }

        private void ShowClip_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ClippingViewModel.ClipItem clip)
                ClippingViewModel.OpenClipLocation(clip);
        }

        private void RenameClip_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not ClippingViewModel.ClipItem clip)
                return;
            _viewModel.BeginRename(clip);
            if (FindAncestor<Border>((DependencyObject)sender) is Border card && FindChild<TextBox>(card, "RenameBox") is TextBox box)
                Dispatcher.BeginInvoke(() => { box.Focus(); box.SelectAll(); });
        }

        private void RenameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not ClippingViewModel.ClipItem clip)
                return;
            if (e.Key == Key.Enter) { _viewModel.CommitRename(clip); e.Handled = true; }
            else if (e.Key == Key.Escape) { _viewModel.CancelRename(clip); e.Handled = true; }
        }

        private void RenameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ClippingViewModel.ClipItem clip && clip.IsRenaming)
                _viewModel.CommitRename(clip);
        }

        private void OpenPlaybacks_Click(object sender, RoutedEventArgs e) => ClippingViewModel.OpenPlaybacksFolder();

        private static T? FindChild<T>(DependencyObject parent, string? name = null) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed && (name is null || typed.Name == name)) return typed;
                T? nested = FindChild<T>(child, name);
                if (nested is not null) return nested;
            }
            return null;
        }

        private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? current = child;
            while (current is not null)
            {
                if (current is T typed) return typed;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
