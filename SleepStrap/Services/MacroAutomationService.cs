using System.Runtime.InteropServices;
using System.ComponentModel;

namespace SleepStrap.Services
{
    public enum MacroWeaponCategory
    {
        Primary,
        Secondary,
        Melee,
        Utility
    }

    public readonly record struct MacroPoint(int X, int Y);

    public static class MacroAutomationService
    {
        // The supplied RIVALS recording was captured on a 1920x1080 monitor positioned
        // immediately left of the primary display. Convert those desktop coordinates
        // into the active Roblox client area so the macro works on any monitor layout.
        private const int RecordedScreenLeft = -1920;
        private const int RecordedScreenTop = 0;
        private const int RecordedScreenWidth = 1920;
        private const int RecordedScreenHeight = 1080;
        private const int TargetClientWidth = 1920;
        private const int TargetClientHeight = 1080;
        private const int SwRestore = 9;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint MonitorDefaultToNearest = 0x00000002;
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const uint MouseEventMove = 0x0001;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint MouseEventVirtualDesk = 0x4000;
        private const uint MouseEventAbsolute = 0x8000;

        private static readonly IReadOnlyDictionary<MacroWeaponCategory, MacroPoint[]> Slots =
            new Dictionary<MacroWeaponCategory, MacroPoint[]>
            {
                [MacroWeaponCategory.Primary] = new[]
                {
                    new MacroPoint(-1119, 577), new MacroPoint(-961, 569), new MacroPoint(-820, 574),
                    new MacroPoint(-665, 574), new MacroPoint(-1259, 729), new MacroPoint(-1108, 715),
                    new MacroPoint(-960, 721), new MacroPoint(-832, 718), new MacroPoint(-667, 722),
                    new MacroPoint(-1267, 882), new MacroPoint(-1116, 856), new MacroPoint(-970, 862),
                    new MacroPoint(-836, 861), new MacroPoint(-648, 860), new MacroPoint(-1261, 1032)
                },
                [MacroWeaponCategory.Secondary] = new[]
                {
                    new MacroPoint(-1119, 598), new MacroPoint(-945, 578), new MacroPoint(-816, 589),
                    new MacroPoint(-669, 583), new MacroPoint(-1276, 738), new MacroPoint(-1077, 713),
                    new MacroPoint(-959, 719), new MacroPoint(-804, 716), new MacroPoint(-665, 721),
                    new MacroPoint(-1271, 895), new MacroPoint(-1110, 890)
                },
                [MacroWeaponCategory.Melee] = new[]
                {
                    new MacroPoint(-1121, 587), new MacroPoint(-994, 578), new MacroPoint(-814, 578),
                    new MacroPoint(-663, 581), new MacroPoint(-1277, 726), new MacroPoint(-1117, 733),
                    new MacroPoint(-978, 727), new MacroPoint(-803, 730), new MacroPoint(-662, 730),
                    new MacroPoint(-1260, 865)
                },
                [MacroWeaponCategory.Utility] = new[]
                {
                    new MacroPoint(-1109, 582), new MacroPoint(-991, 576), new MacroPoint(-806, 572),
                    new MacroPoint(-666, 571), new MacroPoint(-1261, 735), new MacroPoint(-1135, 724),
                    new MacroPoint(-959, 735), new MacroPoint(-760, 737), new MacroPoint(-684, 747),
                    new MacroPoint(-1259, 880), new MacroPoint(-1143, 877), new MacroPoint(-951, 870)
                }
            };

        public static MacroPoint ResolveSlot(
            MacroWeaponCategory category,
            int originalIndex,
            IEnumerable<int> missingIndices)
        {
            int shift = missingIndices.Count(index => index < originalIndex);
            int effectiveIndex = Math.Clamp(originalIndex - shift, 0, Slots[category].Length - 1);
            return Slots[category][effectiveIndex];
        }

        public static async Task RunLoadoutAsync(
            IReadOnlyList<(MacroWeaponCategory Category, int OriginalIndex, IReadOnlyList<int> MissingIndices)> selections,
            CancellationToken cancellationToken)
        {
            IntPtr robloxWindow = FindRobloxWindow();
            if (robloxWindow == IntPtr.Zero)
                throw new InvalidOperationException("Roblox is not running.");

            await NormalizeRobloxWindowAsync(robloxWindow, cancellationToken);

            // Do not send SW_RESTORE to a fullscreen or maximized Roblox window. Windows can
            // interpret that as a request to leave its current presentation state.
            if (IsIconic(robloxWindow))
                ShowWindowAsync(robloxWindow, SwRestore);
            ActivateWindow(robloxWindow);

            DateTime activationDeadline = DateTime.UtcNow.AddSeconds(3);
            while (GetForegroundWindow() != robloxWindow && DateTime.UtcNow < activationDeadline)
            {
                ActivateWindow(robloxWindow);
                await Task.Delay(50, cancellationToken);
            }
            if (GetForegroundWindow() != robloxWindow)
                throw new InvalidOperationException("SleepStrap could not activate the Roblox window.");

            await Task.Delay(150, cancellationToken);

            bool selectorOpen = false;
            try
            {
                App.Logger.WriteLine("MacroAutomationService", "Pressing Ctrl + Alt + Shift + R to open the selector");
                TapChord(0x11, 0x12, 0x10, 0x52); // Ctrl + Alt + Shift + R
                selectorOpen = true;
                await Task.Delay(350, cancellationToken);

                for (int index = 0; index < selections.Count; index++)
                {
                    if (index == 3)
                        await Task.Delay(650, cancellationToken);

                    var selection = selections[index];
                    MacroPoint point = ResolveSlot(selection.Category, selection.OriginalIndex, selection.MissingIndices);
                    point = MapRecordedPointToWindow(point, robloxWindow);
                    await MoveAndClickExactPointAsync(point, cancellationToken);
                    await Task.Delay(index < 3 ? 180 : 250, cancellationToken);
                }

                await Task.Delay(80, cancellationToken);
                TapKey(0x20);
                await Task.Delay(80, cancellationToken);
                TapKey(0x20);
                await Task.Delay(120, cancellationToken);
                App.Logger.WriteLine("MacroAutomationService", "Pressing Ctrl + Alt + Shift + R to close the selector");
                TapChord(0x11, 0x12, 0x10, 0x52);
                selectorOpen = false;
            }
            catch (OperationCanceledException)
            {
                if (selectorOpen && IsRobloxForeground())
                    TapChord(0x11, 0x12, 0x10, 0x52);
                throw;
            }
        }

        public static bool IsRobloxForeground()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foreground, out uint processId);
            if (processId == 0)
                return false;

            try
            {
                using Process process = Process.GetProcessById((int)processId);
                return process.ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

        public static async Task RunAutoRejoinAsync(CancellationToken cancellationToken)
        {
            IntPtr robloxWindow = FindRobloxWindow();
            if (robloxWindow == IntPtr.Zero)
                throw new InvalidOperationException("Roblox is not running.");

            if (IsIconic(robloxWindow))
                ShowWindowAsync(robloxWindow, 9);
            ActivateWindow(robloxWindow);

            App.Logger.WriteLine("MacroAutomationService", "Starting built-in hourly Auto Rejoin sequence");
            TapKey(0x1B); // Escape
            await Task.Delay(120, cancellationToken);
            TapKey(0x4C); // L
            await Task.Delay(120, cancellationToken);
            TapKey(0x0D); // Enter
            await Task.Delay(1400, cancellationToken);

            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-860, 694), 900, cancellationToken); // Disconnect
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-1892, 159), 900, cancellationToken); // Home
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-1735, 1031), 900, cancellationToken); // Select Rivals
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-1011, 372), 1800, cancellationToken); // Join Rivals
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-956, 954), 1200, cancellationToken); // Play
            await DragRecordedPointsAsync(robloxWindow, new MacroPoint(-240, 389), new MacroPoint(-241, 951), cancellationToken);
            await Task.Delay(600, cancellationToken);
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-977, 827), 700, cancellationToken); // Select FFA
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-790, 836), 500, cancellationToken); // Join
        }

        public static void TapKey(byte virtualKey)
        {
            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKey, 0, 2, UIntPtr.Zero);
        }

        private static void TapChord(params byte[] keys)
        {
            foreach (byte key in keys)
                keybd_event(key, 0, 0, UIntPtr.Zero);
            for (int index = keys.Length - 1; index >= 0; index--)
                keybd_event(keys[index], 0, 2, UIntPtr.Zero);
        }

        private static async Task MoveAndClickExactPointAsync(MacroPoint point, CancellationToken cancellationToken)
        {
            App.Logger.WriteLine("MacroAutomationService", $"Moving cursor to recorded screen point {point.X}, {point.Y}");
            App.Logger.WriteLine("MacroAutomationService", $"Clicking recorded screen point {point.X}, {point.Y}");
            SendMouseAtPoint(point, MouseEventLeftDown);
            try
            {
                await Task.Delay(25, cancellationToken);
            }
            finally
            {
                SendMouseButton(MouseEventLeftUp);
            }
        }

        private static async Task ClickRecordedPointAsync(IntPtr window, MacroPoint recordedPoint, int waitAfter, CancellationToken cancellationToken)
        {
            await MoveAndClickExactPointAsync(MapRecordedPointToWindow(recordedPoint, window), cancellationToken);
            await Task.Delay(waitAfter, cancellationToken);
        }

        private static async Task DragRecordedPointsAsync(IntPtr window, MacroPoint recordedStart, MacroPoint recordedEnd, CancellationToken cancellationToken)
        {
            MacroPoint start = MapRecordedPointToWindow(recordedStart, window);
            MacroPoint end = MapRecordedPointToWindow(recordedEnd, window);
            if (!SetCursorPos(start.X, start.Y))
                throw new InvalidOperationException("SleepStrap could not start the Auto Rejoin drag.");
            await Task.Delay(80, cancellationToken);
            SendMouseButton(MouseEventLeftDown);
            try
            {
                await Task.Delay(120, cancellationToken);
                SetCursorPos(end.X, end.Y);
                await Task.Delay(100, cancellationToken);
            }
            finally
            {
                SendMouseButton(MouseEventLeftUp);
            }
        }

        private static MacroPoint MapRecordedPointToWindow(MacroPoint recordedPoint, IntPtr window)
        {
            if (!GetClientRect(window, out RECT clientRect))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the Roblox window size.");

            POINT clientOrigin = new() { X = clientRect.Left, Y = clientRect.Top };
            if (!ClientToScreen(window, ref clientOrigin))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not locate the Roblox window.");

            int clientWidth = clientRect.Right - clientRect.Left;
            int clientHeight = clientRect.Bottom - clientRect.Top;
            if (clientWidth <= 0 || clientHeight <= 0)
                throw new InvalidOperationException("The Roblox window has no usable client area.");

            double relativeX = (recordedPoint.X - RecordedScreenLeft) / (double)RecordedScreenWidth;
            double relativeY = (recordedPoint.Y - RecordedScreenTop) / (double)RecordedScreenHeight;
            int mappedX = clientOrigin.X + (int)Math.Round(relativeX * clientWidth);
            int mappedY = clientOrigin.Y + (int)Math.Round(relativeY * clientHeight);

            mappedX = Math.Clamp(mappedX, clientOrigin.X, clientOrigin.X + clientWidth - 1);
            mappedY = Math.Clamp(mappedY, clientOrigin.Y, clientOrigin.Y + clientHeight - 1);
            App.Logger.WriteLine(
                "MacroAutomationService",
                $"Mapped recorded point {recordedPoint.X}, {recordedPoint.Y} to Roblox client point {mappedX}, {mappedY}");
            return new MacroPoint(mappedX, mappedY);
        }

        private static async Task NormalizeRobloxWindowAsync(IntPtr window, CancellationToken cancellationToken)
        {
            if (!GetClientRect(window, out RECT currentClient))
                return;

            int currentWidth = currentClient.Right - currentClient.Left;
            int currentHeight = currentClient.Bottom - currentClient.Top;
            if (currentWidth == TargetClientWidth && currentHeight == TargetClientHeight)
                return;

            // Restore down rather than minimize to the taskbar: a minimized window cannot
            // receive the selector hotkey or clicks. The coordinate mapper below still
            // scales to the final client size if the monitor cannot fit full 1080p.
            ShowWindowAsync(window, SwRestore);
            await Task.Delay(150, cancellationToken);

            IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            MONITORINFO monitorInfo = new() { Size = Marshal.SizeOf<MONITORINFO>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
                return;

            int style = GetWindowLong(window, GwlStyle);
            int extendedStyle = GetWindowLong(window, GwlExStyle);
            RECT targetFrame = new() { Right = TargetClientWidth, Bottom = TargetClientHeight };
            if (!AdjustWindowRectEx(ref targetFrame, style, false, extendedStyle))
                return;

            int chromeWidth = (targetFrame.Right - targetFrame.Left) - TargetClientWidth;
            int chromeHeight = (targetFrame.Bottom - targetFrame.Top) - TargetClientHeight;
            int workWidth = monitorInfo.Work.Right - monitorInfo.Work.Left;
            int workHeight = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
            int availableClientWidth = Math.Max(640, workWidth - Math.Max(0, chromeWidth));
            int availableClientHeight = Math.Max(360, workHeight - Math.Max(0, chromeHeight));
            double scale = Math.Min(
                1d,
                Math.Min(availableClientWidth / (double)TargetClientWidth, availableClientHeight / (double)TargetClientHeight));
            int targetClientWidth = Math.Max(640, (int)Math.Floor(TargetClientWidth * scale / 2d) * 2);
            int targetClientHeight = Math.Max(360, (int)Math.Floor(TargetClientHeight * scale / 2d) * 2);

            targetFrame = new RECT { Right = targetClientWidth, Bottom = targetClientHeight };
            if (!AdjustWindowRectEx(ref targetFrame, style, false, extendedStyle))
                return;

            int outerWidth = targetFrame.Right - targetFrame.Left;
            int outerHeight = targetFrame.Bottom - targetFrame.Top;
            int x = monitorInfo.Work.Left + Math.Max(0, (workWidth - outerWidth) / 2);
            int y = monitorInfo.Work.Top + Math.Max(0, (workHeight - outerHeight) / 2);
            if (!SetWindowPos(window, IntPtr.Zero, x, y, outerWidth, outerHeight, SwpNoZOrder | SwpNoActivate))
                return;

            await Task.Delay(150, cancellationToken);
            if (GetClientRect(window, out RECT resizedClient))
            {
                App.Logger.WriteLine(
                    "MacroAutomationService",
                    $"Restored and scaled Roblox client from {currentWidth}x{currentHeight} to " +
                    $"{resizedClient.Right - resizedClient.Left}x{resizedClient.Bottom - resizedClient.Top}");
            }
        }

        private static void SendMouseButton(uint flags)
        {
            INPUT[] inputs =
            {
                new INPUT
                {
                    Type = 0,
                    Data = new INPUTUNION
                    {
                        Mouse = new MOUSEINPUT { Flags = flags }
                    }
                }
            };

            if (SendInput(1, inputs, Marshal.SizeOf<INPUT>()) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the simulated mouse click.");
        }

        private static void SendMouseAtPoint(MacroPoint point, uint buttonFlags)
        {
            int virtualLeft = GetSystemMetrics(76);
            int virtualTop = GetSystemMetrics(77);
            int virtualWidth = Math.Max(2, GetSystemMetrics(78));
            int virtualHeight = Math.Max(2, GetSystemMetrics(79));
            int absoluteX = (int)Math.Round((point.X - virtualLeft) * 65535d / (virtualWidth - 1));
            int absoluteY = (int)Math.Round((point.Y - virtualTop) * 65535d / (virtualHeight - 1));

            INPUT[] inputs =
            {
                new INPUT
                {
                    Type = 0,
                    Data = new INPUTUNION
                    {
                        Mouse = new MOUSEINPUT
                        {
                            X = Math.Clamp(absoluteX, 0, 65535),
                            Y = Math.Clamp(absoluteY, 0, 65535),
                            Flags = MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk
                        }
                    }
                },
                new INPUT
                {
                    Type = 0,
                    Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = buttonFlags } }
                }
            };

            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != (uint)inputs.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the simulated mouse click.");
        }

        private static void ActivateWindow(IntPtr window)
        {
            IntPtr foreground = GetForegroundWindow();
            uint currentThread = GetCurrentThreadId();
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
            bool attached = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);

            try
            {
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        private static IntPtr FindRobloxWindow()
        {
            foreach (Process process in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        return process.MainWindowHandle;
                }
                finally
                {
                    process.Dispose();
                }
            }

            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT Work;
            public uint Flags;
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

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr window, ref POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRectEx(ref RECT rectangle, int style, bool hasMenu, int extendedStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO monitorInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    }
}
