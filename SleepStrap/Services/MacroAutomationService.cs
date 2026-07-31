using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        private const int RecordedScreenLeft = -1920;
        private const int RecordedScreenTop = 0;
        private const int RecordedScreenWidth = 1920;
        private const int RecordedScreenHeight = 1080;
        private const int SwRestore = 9;
        private const uint MouseEventMove = 0x0001;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint MouseEventVirtualDesk = 0x4000;
        private const uint MouseEventAbsolute = 0x8000;

        private static readonly IReadOnlyDictionary<MacroWeaponCategory, MacroPoint[]> GridSlots =
            new Dictionary<MacroWeaponCategory, MacroPoint[]>
            {
                [MacroWeaponCategory.Primary] = new[]
                {
                    new MacroPoint(-1120, 577), new MacroPoint(-958, 573), new MacroPoint(-826, 573),
                    new MacroPoint(-701, 571), new MacroPoint(-1268, 730), new MacroPoint(-1126, 724),
                    new MacroPoint(-963, 722), new MacroPoint(-839, 719), new MacroPoint(-661, 711),
                    new MacroPoint(-1232, 868), new MacroPoint(-1101, 866), new MacroPoint(-980, 862),
                    new MacroPoint(-849, 859), new MacroPoint(-706, 866), new MacroPoint(-1279, 1047)
                },
                [MacroWeaponCategory.Secondary] = new[]
                {
                    new MacroPoint(-1120, 604), new MacroPoint(-952, 584), new MacroPoint(-834, 582),
                    new MacroPoint(-674, 585), new MacroPoint(-1271, 723), new MacroPoint(-1117, 716),
                    new MacroPoint(-942, 709), new MacroPoint(-832, 711), new MacroPoint(-704, 716),
                    new MacroPoint(-1273, 898), new MacroPoint(-1131, 883)
                },
                [MacroWeaponCategory.Melee] = new[]
                {
                    new MacroPoint(-1117, 568), new MacroPoint(-959, 574), new MacroPoint(-819, 575),
                    new MacroPoint(-692, 584), new MacroPoint(-1268, 725), new MacroPoint(-1118, 725),
                    new MacroPoint(-993, 725), new MacroPoint(-832, 725), new MacroPoint(-660, 722),
                    new MacroPoint(-1259, 891)
                },
                [MacroWeaponCategory.Utility] = new[]
                {
                    new MacroPoint(-1147, 591), new MacroPoint(-986, 589), new MacroPoint(-809, 592),
                    new MacroPoint(-697, 587), new MacroPoint(-1272, 761), new MacroPoint(-1141, 748),
                    new MacroPoint(-937, 736), new MacroPoint(-823, 736), new MacroPoint(-651, 742),
                    new MacroPoint(-1284, 905), new MacroPoint(-1136, 877), new MacroPoint(-979, 873)
                }
            };

        public static MacroPoint ResolveGridSlot(
            MacroWeaponCategory category,
            int originalIndex,
            IEnumerable<int> missingIndices)
        {
            int shift = missingIndices.Count(index => index < originalIndex);
            int effectiveIndex = originalIndex - shift;
            MacroPoint[] slots = GridSlots[category];

            if (effectiveIndex < 0 || effectiveIndex >= slots.Length)
                throw new InvalidOperationException("The calibrated weapon position is outside the recorded Grid.");

            return slots[effectiveIndex];
        }

        public static async Task RunGridLoadoutAsync(
            IReadOnlyList<(MacroWeaponCategory Category, int OriginalIndex, IReadOnlyList<int> MissingIndices)> selections,
            CancellationToken cancellationToken)
        {
            if (selections.Count != 4)
                throw new ArgumentException("A complete four-item loadout is required.", nameof(selections));

            IntPtr robloxWindow = FindRobloxWindow();
            if (robloxWindow == IntPtr.Zero)
                throw new InvalidOperationException("Roblox is not running.");

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
                throw new InvalidOperationException($"{App.ProjectName} could not activate the Roblox window.");

            await Task.Delay(180, cancellationToken);
            bool selectorOpen = false;
            try
            {
                TapChord(0x11, 0x12, 0x10, 0x52); // Ctrl + Alt + Shift + R
                selectorOpen = true;
                await Task.Delay(420, cancellationToken);

                for (int index = 0; index < selections.Count; index++)
                {
                    var selection = selections[index];
                    MacroPoint point = ResolveGridSlot(
                        selection.Category,
                        selection.OriginalIndex,
                        selection.MissingIndices);
                    await ClickRecordedPointAsync(robloxWindow, point, index < 3 ? 220 : 300, cancellationToken);
                }

                TapKey(0x20);
                await Task.Delay(90, cancellationToken);
                TapKey(0x20);
                await Task.Delay(140, cancellationToken);
                TapChord(0x11, 0x12, 0x10, 0x52);
                selectorOpen = false;
            }
            catch
            {
                if (selectorOpen && GetForegroundWindow() == robloxWindow)
                    TapChord(0x11, 0x12, 0x10, 0x52);
                throw;
            }
        }

        public static async Task RunAutoRejoinAsync(CancellationToken cancellationToken)
        {
            IntPtr robloxWindow = FindRobloxWindow();
            if (robloxWindow == IntPtr.Zero)
                throw new InvalidOperationException("Roblox is not running.");

            if (IsIconic(robloxWindow))
                ShowWindowAsync(robloxWindow, SwRestore);

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
            await DragRecordedPointsAsync(
                robloxWindow,
                new MacroPoint(-240, 389),
                new MacroPoint(-241, 951),
                cancellationToken);
            await Task.Delay(600, cancellationToken);
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-977, 827), 700, cancellationToken); // Select FFA
            await ClickRecordedPointAsync(robloxWindow, new MacroPoint(-790, 836), 500, cancellationToken); // Join
        }

        private static void TapKey(byte virtualKey)
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

        private static async Task ClickRecordedPointAsync(
            IntPtr window,
            MacroPoint recordedPoint,
            int waitAfter,
            CancellationToken cancellationToken)
        {
            MacroPoint point = MapRecordedPointToWindow(recordedPoint, window);
            App.Logger.WriteLine(
                "MacroAutomationService",
                $"Clicking recorded point {recordedPoint.X}, {recordedPoint.Y} at {point.X}, {point.Y}");

            SendMouseAtPoint(point, MouseEventLeftDown);
            try
            {
                await Task.Delay(25, cancellationToken);
            }
            finally
            {
                SendMouseButton(MouseEventLeftUp);
            }

            await Task.Delay(waitAfter, cancellationToken);
        }

        private static async Task DragRecordedPointsAsync(
            IntPtr window,
            MacroPoint recordedStart,
            MacroPoint recordedEnd,
            CancellationToken cancellationToken)
        {
            MacroPoint start = MapRecordedPointToWindow(recordedStart, window);
            MacroPoint end = MapRecordedPointToWindow(recordedEnd, window);
            if (!SetCursorPos(start.X, start.Y))
                throw new InvalidOperationException($"{App.ProjectName} could not start the Auto Rejoin drag.");

            await Task.Delay(80, cancellationToken);
            SendMouseButton(MouseEventLeftDown);
            try
            {
                await Task.Delay(120, cancellationToken);
                if (!SetCursorPos(end.X, end.Y))
                    throw new InvalidOperationException($"{App.ProjectName} could not finish the Auto Rejoin drag.");
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

            return new MacroPoint(
                Math.Clamp(mappedX, clientOrigin.X, clientOrigin.X + clientWidth - 1),
                Math.Clamp(mappedY, clientOrigin.Y, clientOrigin.Y + clientHeight - 1));
        }

        private static void SendMouseButton(uint flags)
        {
            INPUT[] inputs =
            {
                new()
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
                new()
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
                new()
                {
                    Type = 0,
                    Data = new INPUTUNION
                    {
                        Mouse = new MOUSEINPUT { Flags = buttonFlags }
                    }
                }
            };

            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != (uint)inputs.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the simulated mouse click.");
        }

        private static void ActivateWindow(IntPtr window)
        {
            IntPtr foreground = GetForegroundWindow();
            uint currentThread = GetCurrentThreadId();
            uint foregroundThread = foreground == IntPtr.Zero
                ? 0
                : GetWindowThreadProcessId(foreground, out _);
            bool attached = foregroundThread != 0 &&
                            foregroundThread != currentThread &&
                            AttachThreadInput(currentThread, foregroundThread, true);

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
        private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    }
}
