using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class ExperimentalPage
    {
        private readonly ExperimentalViewModel _viewModel;
        private bool _capturingHotkey;
        private int _pendingModifiers;
        private int _pendingVirtualKey;

        public ExperimentalPage()
        {
            _viewModel = new ExperimentalViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void AutoClickerHotkeyBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _capturingHotkey = true;
            _pendingModifiers = 0;
            _pendingVirtualKey = 0;
            AutoClickerHotkeyBox.Text = "Press keys, then Enter";
            AutoClickerHotkeyBox.Focus();
            Keyboard.Focus(AutoClickerHotkeyBox);
            e.Handled = true;
        }

        private void AutoClickerHotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingHotkey)
                return;

            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Enter)
            {
                if (_pendingVirtualKey != 0)
                    _viewModel.SetAutoClickerHotkey(_pendingModifiers, _pendingVirtualKey);
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
            AutoClickerHotkeyBox.Text = FormatPendingHotkey(key, modifiers) + "  •  press Enter";
        }

        private void AutoClickerHotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_capturingHotkey)
                FinishHotkeyCapture();
        }

        private void FinishHotkeyCapture()
        {
            _capturingHotkey = false;
            AutoClickerHotkeyBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
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

        private void CustomSkyTextures_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void CustomSkyTextures_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                _viewModel.SetCustomSkyboxTextureFiles(paths);
            e.Handled = true;
        }
    }
}
