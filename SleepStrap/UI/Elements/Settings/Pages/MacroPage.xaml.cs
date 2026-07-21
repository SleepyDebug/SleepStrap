using System.Windows.Controls;
using System.Windows.Input;

using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class MacroPage
    {
        private readonly MacroViewModel _viewModel;
        private bool _capturingQuickHotkey;
        private int _pendingModifiers;
        private int _pendingVirtualKey;

        public MacroPage()
        {
            _viewModel = new MacroViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            bool sevenDown = Keyboard.IsKeyDown(Key.D7) || Keyboard.IsKeyDown(Key.NumPad7);
            bool revealChord = ((key == Key.D7 || key == Key.NumPad7) && Keyboard.IsKeyDown(Key.U)) ||
                               (key == Key.U && sevenDown);
            if (!revealChord)
                return;

            _viewModel.RevealQuickLoadout();
            e.Handled = true;
        }

        private void QuickHotkeyBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _capturingQuickHotkey = true;
            _viewModel.SetQuickHotkeyCaptureActive(true);
            _pendingModifiers = 0;
            _pendingVirtualKey = 0;
            QuickHotkeyBox.Text = "Press keys, then Enter";
            QuickHotkeyBox.Focus();
            Keyboard.Focus(QuickHotkeyBox);
            e.Handled = true;
        }

        private void QuickHotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingQuickHotkey)
                return;

            e.Handled = true;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Enter)
            {
                if (_pendingVirtualKey != 0)
                    _viewModel.SetQuickLoadoutHotkey(_pendingModifiers, _pendingVirtualKey);
                FinishQuickHotkeyCapture();
                return;
            }
            if (key == Key.Escape)
            {
                FinishQuickHotkeyCapture();
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
            QuickHotkeyBox.Text = FormatHotkey(key, modifiers) + "  •  press Enter";
        }

        private void QuickHotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_capturingQuickHotkey)
                FinishQuickHotkeyCapture();
        }

        private void FinishQuickHotkeyCapture()
        {
            _capturingQuickHotkey = false;
            _viewModel.SetQuickHotkeyCaptureActive(false);
            QuickHotkeyBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }

        private static string FormatHotkey(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(key.ToString());
            return String.Join(" + ", parts);
        }
    }
}
