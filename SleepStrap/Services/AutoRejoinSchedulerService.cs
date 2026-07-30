using System.Windows;
using System.Windows.Threading;

namespace SleepStrap.Services
{
    public static class AutoRejoinSchedulerService
    {
        private static readonly DispatcherTimer Timer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        private static DateTime? _nextRunUtc;
        private static bool _initialized;
        private static bool _running;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            Timer.Tick += Timer_Tick;
            Timer.Start();
            ResetSchedule();
        }

        public static void SetEnabled(bool enabled)
        {
            App.Settings.Prop.MacroAutoRejoinHourly = enabled;
            App.Settings.Save();
            ResetSchedule();
        }

        private static void ResetSchedule()
        {
            _nextRunUtc = App.Settings.Prop.MacroAutoRejoinHourly
                ? DateTime.UtcNow.AddHours(1)
                : null;
        }

        private static void Timer_Tick(object? sender, EventArgs e)
        {
            if (_running || !App.Settings.Prop.MacroAutoRejoinHourly ||
                _nextRunUtc is not DateTime nextRun || DateTime.UtcNow < nextRun)
            {
                return;
            }

            _nextRunUtc = DateTime.UtcNow.AddHours(1);
            _ = RunAsync();
        }

        private static async Task RunAsync()
        {
            _running = true;
            try
            {
                await MacroAutomationService.RunAutoRejoinAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AutoRejoinSchedulerService", ex);
                Frontend.ShowMessageBox(
                    $"SleepStrap could not complete Auto Rejoin.\n\n{ex.Message}",
                    MessageBoxImage.Error);
            }
            finally
            {
                _running = false;
            }
        }
    }
}
