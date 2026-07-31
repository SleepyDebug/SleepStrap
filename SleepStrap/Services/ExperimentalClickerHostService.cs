using System.Diagnostics;
using System.Threading;

namespace SleepStrap.Services
{
    /// <summary>
    /// Hosts experimental input features separately from the settings window so
    /// their configured hotkey still works while Roblox is focused.
    /// </summary>
    internal static class ExperimentalClickerHostService
    {
        private const string InstanceMutexName = "SleepStrap-Experimental-Input-Instance-V1";
        private const string StopEventName = "SleepStrap-Experimental-Input-Stop-V1";

        private static Mutex? _instanceMutex;
        private static EventWaitHandle? _stopEvent;
        private static RegisteredWaitHandle? _stopRegistration;
        private static ExperimentalClickerService? _clicker;

        public static bool IsConfigured =>
            App.Settings.Prop.ExperimentalAutoClickerEnabled ||
            App.Settings.Prop.ExperimentalRobloxHoldToSpamEnabled;

        public static void EnsureStarted()
        {
            if (App.LaunchSettings.ExperimentalFlag.Active)
                return;

            if (!IsConfigured)
            {
                Stop();
                return;
            }

            if (IsRunning())
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = "-experimental",
                    UseShellExecute = true,
                    WorkingDirectory = Paths.Base
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ExperimentalClickerHostService::EnsureStarted", ex);
                Frontend.ShowMessageBox(
                    $"Experimental input could not start.\n\n{ex.Message}",
                    System.Windows.MessageBoxImage.Error);
            }
        }

        public static void Refresh()
        {
            if (!IsConfigured)
            {
                Stop();
                return;
            }

            if (!IsRunning())
            {
                EnsureStarted();
                return;
            }

            Restart();
        }

        public static void Stop() => SignalEvent(StopEventName);

        public static async void Restart()
        {
            Stop();
            for (int attempt = 0; attempt < 60 && IsRunning(); attempt++)
                await Task.Delay(100);

            EnsureStarted();
        }

        public static bool IsRunning() => IsMutexOpen(InstanceMutexName);

        public static void RunHostMode()
        {
            if (!IsConfigured)
            {
                App.SoftTerminate();
                return;
            }

            _instanceMutex = new Mutex(true, InstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                App.SoftTerminate();
                return;
            }

            _stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StopEventName);
            _clicker = new ExperimentalClickerService();
            _clicker.Start();

            _stopRegistration = ThreadPool.RegisterWaitForSingleObject(
                _stopEvent,
                (_, _) => App.Current.Dispatcher.BeginInvoke(new Action(App.Current.Shutdown)),
                null,
                Timeout.Infinite,
                true);

            App.Current.Exit += (_, _) => DisposeHost();
        }

        private static void DisposeHost()
        {
            _stopRegistration?.Unregister(null);
            _stopRegistration = null;
            _clicker?.Dispose();
            _clicker = null;
            _stopEvent?.Dispose();
            _stopEvent = null;
            _instanceMutex?.Dispose();
            _instanceMutex = null;
        }

        private static bool IsMutexOpen(string name)
        {
            try
            {
                using Mutex mutex = Mutex.OpenExisting(name);
                return true;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }

        private static void SignalEvent(string name)
        {
            try
            {
                using EventWaitHandle stopEvent = EventWaitHandle.OpenExisting(name);
                stopEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // No host is active.
            }
        }
    }
}
