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

        private static readonly object AutomaticActionsLock = new();
        private static Process? _automaticActionsProcess;
        private static string? _automaticActionsScriptPath;

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

            // Do not send SW_RESTORE to a fullscreen or maximized Roblox window. Windows can
            // interpret that as a request to leave its current presentation state.
            if (IsIconic(robloxWindow))
                ShowWindowAsync(robloxWindow, 9);
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

        public static bool ConfigureAutomaticActions(bool quickRespawn, bool autoUtility, bool autoInspect, bool enabled)
        {
            lock (AutomaticActionsLock)
            {
                StopAutomaticActionsCore();

                if (!enabled || (!quickRespawn && !autoUtility && !autoInspect))
                    return FindAutoHotkeyV2() is not null;

                string? autoHotkey = FindAutoHotkeyV2();
                if (autoHotkey is null)
                    return false;

                string scriptPath = Path.Combine(Path.GetTempPath(), $"SleepStrap-AutoActions-{Environment.ProcessId}.ahk");
                string script =
                    "#Requires AutoHotkey v2.0\r\n" +
                    "#SingleInstance Off\r\n" +
                    $"parentPid := {Environment.ProcessId}\r\n" +
                    $"quickRespawn := {(quickRespawn ? "true" : "false")}\r\n" +
                    $"autoUtility := {(autoUtility ? "true" : "false")}\r\n" +
                    $"autoInspect := {(autoInspect ? "true" : "false")}\r\n" +
                    "SetTimer WatchParent, 500\r\n" +
                    "SetTimer SpamQuickRespawn, 75\r\n" +
                    "SetTimer SpamAutoUtility, 120\r\n" +
                    "SetTimer SpamAutoInspect, 180\r\n" +
                    "WatchParent() {\r\n" +
                    "    global parentPid\r\n" +
                    "    if !ProcessExist(parentPid)\r\n" +
                    "        ExitApp\r\n" +
                    "}\r\n" +
                    "RobloxActive() {\r\n" +
                    "    return WinActive(\"ahk_exe RobloxPlayerBeta.exe\")\r\n" +
                    "}\r\n" +
                    "SpamQuickRespawn() {\r\n" +
                    "    global quickRespawn\r\n" +
                    "    if quickRespawn && RobloxActive()\r\n" +
                    "        Send \"{Space}\"\r\n" +
                    "}\r\n" +
                    "SpamAutoUtility() {\r\n" +
                    "    global autoUtility\r\n" +
                    "    if autoUtility && RobloxActive()\r\n" +
                    "        Send \"g\"\r\n" +
                    "}\r\n" +
                    "SpamAutoInspect() {\r\n" +
                    "    global autoInspect\r\n" +
                    "    if autoInspect && RobloxActive()\r\n" +
                    "        Send \"v\"\r\n" +
                    "}\r\n";

                try
                {
                    File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
                    var startInfo = new ProcessStartInfo(autoHotkey)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    startInfo.ArgumentList.Add("/ErrorStdOut");
                    startInfo.ArgumentList.Add(scriptPath);

                    _automaticActionsProcess = Process.Start(startInfo)
                        ?? throw new InvalidOperationException("SleepStrap could not start automatic actions.");
                    _automaticActionsScriptPath = scriptPath;
                    App.Logger.WriteLine("MacroAutomationService", $"Started AutoHotkey automatic actions (Space={quickRespawn}, G={autoUtility}, V={autoInspect})");
                    return true;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("MacroAutomationService::ConfigureAutomaticActions", ex);
                    try { File.Delete(scriptPath); } catch { }
                    return false;
                }
            }
        }

        public static async Task RunAutoRejoinAsync(CancellationToken cancellationToken)
        {
            string? autoHotkey = FindAutoHotkeyV2();
            if (autoHotkey is null)
                throw new InvalidOperationException("Auto Rejoin requires AutoHotkey v2.");

            IntPtr robloxWindow = FindRobloxWindow();
            if (robloxWindow == IntPtr.Zero)
                throw new InvalidOperationException("Roblox is not running.");

            if (IsIconic(robloxWindow))
                ShowWindowAsync(robloxWindow, 9);
            ActivateWindow(robloxWindow);

            string scriptPath = Path.Combine(Path.GetTempPath(), $"SleepStrap-AutoRejoin-{Guid.NewGuid():N}.ahk");
            string script =
                "#Requires AutoHotkey v2.0\r\n" +
                "#SingleInstance Off\r\n" +
                "CoordMode \"Mouse\", \"Screen\"\r\n" +
                "WinActivate \"ahk_exe RobloxPlayerBeta.exe\"\r\n" +
                "if !WinWaitActive(\"ahk_exe RobloxPlayerBeta.exe\",, 3)\r\n" +
                "    ExitApp 2\r\n" +
                "Send \"{Escape}\"\r\n" +
                "Sleep 120\r\n" +
                "Send \"l\"\r\n" +
                "Sleep 120\r\n" +
                "Send \"{Enter}\"\r\n" +
                "Sleep 1400\r\n" +
                "ClickPoint(-860, 694, 900) ; Disconnect\r\n" +
                "ClickPoint(-1892, 159, 900) ; Home\r\n" +
                "ClickPoint(-1735, 1031, 900) ; Select Rivals\r\n" +
                "ClickPoint(-1011, 372, 1800) ; Join Rivals\r\n" +
                "ClickPoint(-956, 954, 1200) ; Play\r\n" +
                "MouseMove -240, 389, 0\r\n" +
                "Sleep 80\r\n" +
                "Click \"Down\"\r\n" +
                "Sleep 120\r\n" +
                "MouseMove -241, 951, 10\r\n" +
                "Sleep 100\r\n" +
                "Click \"Up\"\r\n" +
                "Sleep 600\r\n" +
                "ClickPoint(-977, 827, 700) ; Select FFA\r\n" +
                "ClickPoint(-790, 836, 500) ; Join\r\n" +
                "ExitApp\r\n" +
                "ClickPoint(x, y, waitAfter) {\r\n" +
                "    MouseMove x, y, 0\r\n" +
                "    Sleep 60\r\n" +
                "    Click \"Down\"\r\n" +
                "    Sleep 35\r\n" +
                "    Click \"Up\"\r\n" +
                "    Sleep waitAfter\r\n" +
                "}\r\n";

            try
            {
                await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken);
                App.Logger.WriteLine("MacroAutomationService", "Starting hourly Auto Rejoin sequence");
                var startInfo = new ProcessStartInfo(autoHotkey)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                startInfo.ArgumentList.Add("/ErrorStdOut");
                startInfo.ArgumentList.Add(scriptPath);

                using Process process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("SleepStrap could not start Auto Rejoin.");
                await process.WaitForExitAsync(cancellationToken);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Auto Rejoin stopped with exit code {process.ExitCode}.");
            }
            finally
            {
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
            }
        }

        public static void StopAutomaticActions()
        {
            lock (AutomaticActionsLock)
                StopAutomaticActionsCore();
        }

        private static void StopAutomaticActionsCore()
        {
            try
            {
                if (_automaticActionsProcess is { HasExited: false })
                    _automaticActionsProcess.Kill();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("MacroAutomationService::StopAutomaticActions", ex);
            }
            finally
            {
                _automaticActionsProcess?.Dispose();
                _automaticActionsProcess = null;
            }

            try
            {
                if (_automaticActionsScriptPath is not null && File.Exists(_automaticActionsScriptPath))
                    File.Delete(_automaticActionsScriptPath);
            }
            catch { }
            _automaticActionsScriptPath = null;
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
            string? autoHotkey = FindAutoHotkeyV2();
            if (autoHotkey is not null)
            {
                await ClickWithAutoHotkeyAsync(autoHotkey, point, cancellationToken);
                return;
            }

            App.Logger.WriteLine("MacroAutomationService", $"Moving cursor to recorded screen point {point.X}, {point.Y}");
            if (!SetCursorPos(point.X, point.Y))
                throw new InvalidOperationException("SleepStrap could not move the cursor to the selected weapon.");

            if (!GetCursorPos(out POINT actual) || actual.X != point.X || actual.Y != point.Y)
                throw new InvalidOperationException($"Windows moved the cursor to {actual.X}, {actual.Y} instead of the recorded position {point.X}, {point.Y}.");

            await Task.Delay(45, cancellationToken);

            // The recorder only captures a hover position. Playback owns the click, so lock
            // the cursor back onto that exact point immediately before pressing the button.
            if (!SetCursorPos(point.X, point.Y) ||
                !GetCursorPos(out actual) ||
                actual.X != point.X ||
                actual.Y != point.Y)
            {
                throw new InvalidOperationException($"The cursor left the recorded position before SleepStrap could click {point.X}, {point.Y}.");
            }

            App.Logger.WriteLine("MacroAutomationService", $"Clicking recorded screen point {point.X}, {point.Y}");
            SendMouseButton(0x0002);
            try
            {
                await Task.Delay(25, cancellationToken);
            }
            finally
            {
                SendMouseButton(0x0004);
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

        private static string? FindAutoHotkeyV2()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates =
            {
                Path.Combine(programFiles, "AutoHotkey", "v2", "AutoHotkey64.exe"),
                Path.Combine(localAppData, "Programs", "AutoHotkey", "v2", "AutoHotkey64.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static async Task ClickWithAutoHotkeyAsync(string autoHotkey, MacroPoint point, CancellationToken cancellationToken)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), $"SleepStrap-Macro-{Guid.NewGuid():N}.ahk");
            string script =
                "#Requires AutoHotkey v2.0\r\n" +
                "#SingleInstance Off\r\n" +
                "CoordMode \"Mouse\", \"Screen\"\r\n" +
                $"MouseMove {point.X}, {point.Y}, 0\r\n" +
                "Sleep 45\r\n" +
                "Click \"Down\"\r\n" +
                "Sleep 25\r\n" +
                "Click \"Up\"\r\n" +
                "ExitApp\r\n";

            try
            {
                await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken);
                App.Logger.WriteLine("MacroAutomationService", $"AutoHotkey moving and clicking recorded screen point {point.X}, {point.Y}");

                var startInfo = new ProcessStartInfo(autoHotkey)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                startInfo.ArgumentList.Add("/ErrorStdOut");
                startInfo.ArgumentList.Add(scriptPath);

                using Process process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("SleepStrap could not start AutoHotkey for the recorded click.");
                await process.WaitForExitAsync(cancellationToken);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"AutoHotkey could not click the recorded position (exit code {process.ExitCode}).");
            }
            finally
            {
                try
                {
                    if (File.Exists(scriptPath))
                        File.Delete(scriptPath);
                }
                catch
                {
                    // The temporary script is harmless and Windows will clear the temp folder later.
                }
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

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr window, ref POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    }
}
