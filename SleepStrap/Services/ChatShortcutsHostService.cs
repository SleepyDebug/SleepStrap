using System.Diagnostics;
using System.Threading;

namespace SleepStrap.Services
{
    internal static class ChatShortcutsHostService
    {
        private const string InstanceMutexName = "SleepBlox-ChatShortcuts-Instance-V1";
        private const string StopEventName = "SleepBlox-ChatShortcuts-Stop-V1";
        private static Mutex? _mutex;
        private static EventWaitHandle? _stopEvent;
        private static RegisteredWaitHandle? _registration;
        private static ChatShortcutsOverlayService? _service;

        public static bool IsConfigured => App.Settings.Prop.ChatShortcutsEnabled;
        public static void EnsureStarted()
        {
            if (App.LaunchSettings.ChatShortcutsFlag.Active) return;
            if (!IsConfigured) { Stop(); return; }
            if (IsRunning()) return;
            try { Process.Start(new ProcessStartInfo { FileName = Paths.Process, Arguments = "-chatshortcuts", UseShellExecute = true, WorkingDirectory = Paths.Base }); }
            catch (Exception ex) { App.Logger.WriteException("ChatShortcutsHostService::EnsureStarted", ex); }
        }
        public static void Refresh() { if (!IsConfigured) Stop(); else if (!IsRunning()) EnsureStarted(); }
        public static void Stop() => SignalEvent(StopEventName);
        public static void RunHostMode()
        {
            if (!IsConfigured) { App.SoftTerminate(); return; }
            _mutex = new Mutex(true, InstanceMutexName, out bool createdNew);
            if (!createdNew) { App.SoftTerminate(); return; }
            _stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StopEventName);
            _service = new ChatShortcutsOverlayService(); _service.Start();
            _registration = ThreadPool.RegisterWaitForSingleObject(_stopEvent, (_, _) => App.Current.Dispatcher.BeginInvoke(new Action(App.Current.Shutdown)), null, Timeout.Infinite, true);
            App.Current.Exit += (_, _) => DisposeHost();
        }
        private static bool IsRunning() { try { using Mutex mutex = Mutex.OpenExisting(InstanceMutexName); return true; } catch (WaitHandleCannotBeOpenedException) { return false; } }
        private static void SignalEvent(string name) { try { using EventWaitHandle stop = EventWaitHandle.OpenExisting(name); stop.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
        private static void DisposeHost() { _registration?.Unregister(null); _service?.Dispose(); _stopEvent?.Dispose(); _mutex?.Dispose(); _registration = null; _service = null; _stopEvent = null; _mutex = null; }
    }
}
