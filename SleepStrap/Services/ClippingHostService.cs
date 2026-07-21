using System.Diagnostics;
using System.Threading;

namespace SleepStrap.Services
{
    public static class ClippingHostService
    {
        internal const string InstanceMutexName = "SleepStrap-Clipping-Instance-V2";
        internal const string StopEventName = "SleepStrap-Clipping-Stop-V2";
        private const string LegacyInstanceMutexName = "SleepStrap-Clipping-Instance";
        private const string LegacyStopEventName = "SleepStrap-Clipping-Stop";
        private const string RemovedSleePlayerStopEventName = "SleepStrap-SleePlayer-Stop";

        private static Mutex? _instanceMutex;
        private static EventWaitHandle? _stopEvent;
        private static RegisteredWaitHandle? _stopRegistration;
        private static ReplayBufferService? _replayBuffer;

        public static async void EnsureStarted()
        {
            if (App.LaunchSettings.ClippingFlag.Active)
                return;

            SignalEvent(LegacyStopEventName);
            for (int attempt = 0; attempt < 50 && IsMutexOpen(LegacyInstanceMutexName); attempt++)
                await Task.Delay(100);

            if (IsRunning())
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = "-clipping",
                    UseShellExecute = true,
                    WorkingDirectory = Paths.Base
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ClippingHostService::EnsureStarted", ex);
                Frontend.ShowMessageBox($"Clipping could not start.\n\n{ex.Message}", System.Windows.MessageBoxImage.Error);
            }
        }

        public static void Stop()
        {
            SignalEvent(StopEventName);
            SignalEvent(LegacyStopEventName);
        }

        public static async void Restart()
        {
            Stop();
            for (int attempt = 0; attempt < 60 && IsRunning(); attempt++)
                await Task.Delay(100);
            EnsureStarted();
        }

        public static void StopRemovedSleePlayer() => SignalEvent(RemovedSleePlayerStopEventName);

        public static bool IsRunning()
        {
            return IsMutexOpen(InstanceMutexName);
        }

        public static void RunHostMode()
        {
            _instanceMutex = new Mutex(true, InstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                App.SoftTerminate();
                return;
            }

            _stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StopEventName);
            _replayBuffer = new ReplayBufferService();
            _replayBuffer.Start();

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
            _replayBuffer?.Dispose();
            _replayBuffer = null;
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
                // No process owns this event.
            }
        }
    }
}
