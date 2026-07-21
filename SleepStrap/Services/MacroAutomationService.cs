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

    public enum MacroWeaponLayout
    {
        Grid,
        List
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
        private const int SwRestore = 9;
        private const uint MouseEventMove = 0x0001;
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint MouseEventWheel = 0x0800;
        private const uint MouseEventVirtualDesk = 0x4000;
        private const uint MouseEventAbsolute = 0x8000;

        private static readonly IReadOnlyDictionary<MacroWeaponCategory, MacroPoint[]> GridSlots =
            new Dictionary<MacroWeaponCategory, MacroPoint[]>
            {
                [MacroWeaponCategory.Primary] = new[]
                {
                    new MacroPoint(-1115, 571), new MacroPoint(-970, 566), new MacroPoint(-824, 572),
                    new MacroPoint(-682, 576), new MacroPoint(-1267, 726), new MacroPoint(-1114, 725),
                    new MacroPoint(-965, 720), new MacroPoint(-817, 712), new MacroPoint(-657, 725),
                    new MacroPoint(-1269, 863), new MacroPoint(-1111, 865), new MacroPoint(-964, 869),
                    new MacroPoint(-817, 862), new MacroPoint(-680, 860), new MacroPoint(-1268, 1013)
                },
                [MacroWeaponCategory.Secondary] = new[]
                {
                    new MacroPoint(-1116, 574), new MacroPoint(-966, 575), new MacroPoint(-821, 569),
                    new MacroPoint(-673, 566), new MacroPoint(-1263, 719), new MacroPoint(-1111, 717),
                    new MacroPoint(-966, 718), new MacroPoint(-821, 724), new MacroPoint(-666, 714),
                    new MacroPoint(-1264, 876), new MacroPoint(-1105, 875)
                },
                [MacroWeaponCategory.Melee] = new[]
                {
                    new MacroPoint(-1115, 575), new MacroPoint(-968, 581), new MacroPoint(-832, 581),
                    new MacroPoint(-672, 571), new MacroPoint(-1266, 721), new MacroPoint(-1109, 719),
                    new MacroPoint(-970, 719), new MacroPoint(-812, 716), new MacroPoint(-676, 718),
                    new MacroPoint(-1244, 867)
                },
                [MacroWeaponCategory.Utility] = new[]
                {
                    new MacroPoint(-1100, 569), new MacroPoint(-974, 577), new MacroPoint(-814, 587),
                    new MacroPoint(-687, 587), new MacroPoint(-1261, 734), new MacroPoint(-1101, 719),
                    new MacroPoint(-977, 732), new MacroPoint(-793, 731), new MacroPoint(-679, 722),
                    new MacroPoint(-1265, 859), new MacroPoint(-1106, 857), new MacroPoint(-965, 858)
                }
            };

        private static readonly IReadOnlyDictionary<MacroWeaponCategory, MacroPoint[]> ListSlots =
            new Dictionary<MacroWeaponCategory, MacroPoint[]>
            {
                [MacroWeaponCategory.Primary] = new[]
                {
                    new MacroPoint(-968, 492), new MacroPoint(-958, 577), new MacroPoint(-969, 671),
                    new MacroPoint(-965, 740), new MacroPoint(-984, 830), new MacroPoint(-980, 914),
                    new MacroPoint(-974, 997), new MacroPoint(-969, 437), new MacroPoint(-963, 531),
                    new MacroPoint(-960, 602), new MacroPoint(-953, 691), new MacroPoint(-950, 788),
                    new MacroPoint(-952, 877), new MacroPoint(-949, 950), new MacroPoint(-957, 1030)
                },
                [MacroWeaponCategory.Secondary] = new[]
                {
                    new MacroPoint(-962, 477), new MacroPoint(-957, 571), new MacroPoint(-965, 657),
                    new MacroPoint(-956, 741), new MacroPoint(-972, 827), new MacroPoint(-989, 902),
                    new MacroPoint(-955, 992), new MacroPoint(-969, 774), new MacroPoint(-969, 858),
                    new MacroPoint(-972, 949), new MacroPoint(-953, 1022)
                },
                [MacroWeaponCategory.Melee] = new[]
                {
                    new MacroPoint(-962, 489), new MacroPoint(-955, 570), new MacroPoint(-954, 655),
                    new MacroPoint(-954, 740), new MacroPoint(-968, 826), new MacroPoint(-960, 901),
                    new MacroPoint(-962, 1005), new MacroPoint(-970, 850), new MacroPoint(-958, 932),
                    new MacroPoint(-959, 1024)
                },
                [MacroWeaponCategory.Utility] = new[]
                {
                    new MacroPoint(-953, 480), new MacroPoint(-968, 570), new MacroPoint(-960, 643),
                    new MacroPoint(-974, 738), new MacroPoint(-968, 828), new MacroPoint(-952, 914),
                    new MacroPoint(-990, 1009), new MacroPoint(-973, 680), new MacroPoint(-975, 774),
                    new MacroPoint(-966, 855), new MacroPoint(-971, 942), new MacroPoint(-990, 1043)
                }
            };

        public static MacroPoint ResolveSlot(
            MacroWeaponCategory category,
            int originalIndex,
            IEnumerable<int> missingIndices,
            MacroWeaponLayout layout)
        {
            IReadOnlyDictionary<MacroWeaponCategory, MacroPoint[]> slots =
                layout == MacroWeaponLayout.List ? ListSlots : GridSlots;
            int shift = missingIndices.Count(index => index < originalIndex);
            int effectiveIndex = Math.Clamp(originalIndex - shift, 0, slots[category].Length - 1);
            int positionIndex = effectiveIndex;
            return slots[category][positionIndex];
        }

        public static async Task RunLoadoutAsync(
            IReadOnlyList<(MacroWeaponCategory Category, int OriginalIndex, IReadOnlyList<int> MissingIndices)> selections,
            MacroWeaponLayout layout,
            CancellationToken cancellationToken)
        {
            IntPtr robloxWindow = FindRobloxWindow();
            if (robloxWindow == IntPtr.Zero)
                throw new InvalidOperationException("Roblox is not running.");

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
                    MacroPoint point = ResolveSlot(selection.Category, selection.OriginalIndex, selection.MissingIndices, layout);
                    point = MapRecordedPointToWindow(point, robloxWindow);
                    if (layout == MacroWeaponLayout.List)
                    {
                        int missingBefore = selection.MissingIndices.Count(missing => missing < selection.OriginalIndex);
                        int effectiveIndex = Math.Max(0, selection.OriginalIndex - missingBefore);
                        await PrepareListScrollAsync(point, effectiveIndex >= 7, cancellationToken);
                    }
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

        private static async Task PrepareListScrollAsync(MacroPoint point, bool scrollToBottom, CancellationToken cancellationToken)
        {
            App.Logger.WriteLine(
                "MacroAutomationService",
                $"Normalizing List layout at {point.X}, {point.Y}; target edge: {(scrollToBottom ? "bottom" : "top")}");
            if (!SetCursorPos(point.X, point.Y))
                throw new InvalidOperationException("SleepStrap could not position the cursor over the List layout.");

            // Normalize every category to the top first. RIVALS preserves some List
            // scroll positions between openings, so relying on the current offset can
            // select the wrong weapon even when the recorded point is correct.
            await Task.Delay(90, cancellationToken);
            for (int notch = 0; notch < 24; notch++)
            {
                SendMouseWheel(120);
                await Task.Delay(24, cancellationToken);
            }

            await Task.Delay(220, cancellationToken);
            if (!scrollToBottom)
                return;

            // Every recorded weapon from slot eight onward was captured with its
            // category at the bottom edge. Deliberately overscroll so no wheel input
            // dropped by RIVALS can leave the list between pages.
            for (int notch = 0; notch < 30; notch++)
            {
                SendMouseWheel(-120);
                await Task.Delay(28, cancellationToken);
            }

            // Let the animated list settle before the exact recorded click.
            await Task.Delay(420, cancellationToken);
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

        private static void SendMouseWheel(int delta)
        {
            INPUT[] inputs =
            {
                new INPUT
                {
                    Type = 0,
                    Data = new INPUTUNION
                    {
                        Mouse = new MOUSEINPUT
                        {
                            MouseData = unchecked((uint)delta),
                            Flags = MouseEventWheel
                        }
                    }
                }
            };

            if (SendInput(1, inputs, Marshal.SizeOf<INPUT>()) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the simulated List scroll.");
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
        private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    }
}
