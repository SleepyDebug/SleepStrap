using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SleepStrap.Services
{
    /// <summary>
    /// Sends clicks only while the Roblox client has focus. The hold mode consumes
    /// the physical button press, then replaces it with individual injected clicks.
    /// </summary>
    internal sealed class ExperimentalClickerService : IDisposable
    {
        private const int HotkeyId = 0x5353;
        private const int WmHotkey = 0x0312;
        private const int WhMouseLl = 14;
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const uint LlmhfInjected = 0x00000001;
        private const uint ModNoRepeat = 0x4000;
        private const uint InputMouse = 0;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private static readonly UIntPtr InputMarker = new(0x53534C50434C4943UL);

        private readonly CancellationTokenSource _stopToken = new();
        private HwndSource? _hotkeyWindow;
        private LowLevelMouseProc? _mouseHookProc;
        private IntPtr _mouseHook;
        private Task? _clickLoop;
        private int _autoClicking;
        private int _physicalLeftHeld;
        private bool _hotkeyRegistered;
        private bool _disposed;

        public void Start()
        {
            if (App.Settings.Prop.ExperimentalAutoClickerEnabled)
                RegisterHotkey();

            if (App.Settings.Prop.ExperimentalRobloxHoldToSpamEnabled)
                InstallMouseHook();

            _clickLoop = Task.Run(() => ClickLoopAsync(_stopToken.Token));
            App.Logger.WriteLine(
                "ExperimentalClickerService::Start",
                $"Started (auto={App.Settings.Prop.ExperimentalAutoClickerEnabled}, hold={App.Settings.Prop.ExperimentalRobloxHoldToSpamEnabled}).");
        }

        private void RegisterHotkey()
        {
            _hotkeyWindow = new HwndSource(new HwndSourceParameters($"{App.ProjectName} Experimental Hotkey")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0
            });
            _hotkeyWindow.AddHook(HotkeyWindowHook);

            uint modifiers = (uint)App.Settings.Prop.ExperimentalAutoClickerHotkeyModifiers | ModNoRepeat;
            uint virtualKey = (uint)App.Settings.Prop.ExperimentalAutoClickerHotkeyVirtualKey;
            if (!RegisterHotKey(_hotkeyWindow.Handle, HotkeyId, modifiers, virtualKey))
            {
                int error = Marshal.GetLastWin32Error();
                App.Logger.WriteException(
                    "ExperimentalClickerService::RegisterHotkey",
                    new Win32Exception(error, "The configured auto clicker hotkey could not be registered."));
                return;
            }

            _hotkeyRegistered = true;
            App.Logger.WriteLine(
                "ExperimentalClickerService::RegisterHotkey",
                $"Registered global clicker hotkey modifiers=0x{modifiers:X} key=0x{virtualKey:X}");
        }

        private IntPtr HotkeyWindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WmHotkey || wParam.ToInt32() != HotkeyId)
                return IntPtr.Zero;

            handled = true;
            bool enabled = Interlocked.CompareExchange(ref _autoClicking, 0, 0) == 0;
            Interlocked.Exchange(ref _autoClicking, enabled ? 1 : 0);
            App.Logger.WriteLine(
                "ExperimentalClickerService::Hotkey",
                enabled ? "Auto clicker enabled." : "Auto clicker disabled.");
            return IntPtr.Zero;
        }

        private void InstallMouseHook()
        {
            _mouseHookProc = MouseHookCallback;
            IntPtr module = GetModuleHandle(null);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseHookProc, module, 0);
            if (_mouseHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, "Windows could not enable Roblox hold-to-spam.");
            }
        }

        private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || _disposed)
                return CallNextHookEx(_mouseHook, code, wParam, lParam);

            int message = wParam.ToInt32();
            if (message is not (WmLButtonDown or WmLButtonUp))
                return CallNextHookEx(_mouseHook, code, wParam, lParam);

            MSLLHOOKSTRUCT mouse = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((mouse.Flags & LlmhfInjected) != 0 || mouse.ExtraInfo == InputMarker)
                return CallNextHookEx(_mouseHook, code, wParam, lParam);

            if (message == WmLButtonDown)
            {
                if (!IsRobloxForeground())
                    return CallNextHookEx(_mouseHook, code, wParam, lParam);

                Interlocked.Exchange(ref _physicalLeftHeld, 1);
                return new IntPtr(1);
            }

            if (Interlocked.Exchange(ref _physicalLeftHeld, 0) == 1)
                return new IntPtr(1);

            return CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        private async Task ClickLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool autoClicking = Interlocked.CompareExchange(ref _autoClicking, 0, 0) == 1;
                    bool physicalLeftHeld = Interlocked.CompareExchange(ref _physicalLeftHeld, 0, 0) == 1;
                    bool shouldClick = autoClicking || physicalLeftHeld;

                    if (shouldClick && IsRobloxForeground())
                        SendLeftClick();

                    // A physical held click is the hold-to-spam feature, so it has its
                    // own rate even when the regular auto clicker happens to be active.
                    int clicksPerSecond = physicalLeftHeld
                        ? Math.Clamp(App.Settings.Prop.ExperimentalRobloxHoldToSpamClicksPerSecond, 1, 50)
                        : Math.Clamp(App.Settings.Prop.ExperimentalAutoClickerClicksPerSecond, 1, 50);
                    int delay = Math.Max(20, (int)Math.Round(1000d / clicksPerSecond));
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the host exits or settings disable both tools.
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ExperimentalClickerService::ClickLoop", ex);
            }
        }

        private static bool IsRobloxForeground()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(window, out uint processId);
            if (processId == 0)
                return false;

            try
            {
                using Process process = Process.GetProcessById((int)processId);
                return String.Equals(process.ProcessName, "RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void SendLeftClick()
        {
            INPUT[] inputs =
            {
                new()
                {
                    Type = InputMouse,
                    Data = new INPUTUNION
                    {
                        Mouse = new MOUSEINPUT { Flags = MouseEventLeftDown, ExtraInfo = InputMarker }
                    }
                },
                new()
                {
                    Type = InputMouse,
                    Data = new INPUTUNION
                    {
                        Mouse = new MOUSEINPUT { Flags = MouseEventLeftUp, ExtraInfo = InputMarker }
                    }
                }
            };

            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != (uint)inputs.Length)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0)
                    throw new Win32Exception(error, "Windows rejected the simulated mouse click.");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            Interlocked.Exchange(ref _autoClicking, 0);
            Interlocked.Exchange(ref _physicalLeftHeld, 0);
            _stopToken.Cancel();

            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            _mouseHookProc = null;

            if (_hotkeyWindow is not null)
            {
                HwndSource window = _hotkeyWindow;
                void DisposeWindow()
                {
                    if (_hotkeyRegistered)
                        UnregisterHotKey(window.Handle, HotkeyId);
                    window.RemoveHook(HotkeyWindowHook);
                    window.Dispose();
                }

                if (window.Dispatcher.CheckAccess())
                    DisposeWindow();
                else
                    window.Dispatcher.Invoke(DisposeWindow);

                _hotkeyWindow = null;
                _hotkeyRegistered = false;
            }

            _stopToken.Dispose();
        }

        private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public INPUTUNION Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? moduleName);
    }
}
