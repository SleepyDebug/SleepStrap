namespace SleepStrap.Services
{
    internal static class ProcessShutdownService
    {
        private const string LogIdent = "ProcessShutdownService";

        public static int CloseOtherSleepStrapProcesses()
        {
            int currentProcessId = Environment.ProcessId;
            List<Process> processes = GetOtherSleepStrapProcesses(currentProcessId);

            foreach (Process process in processes)
            {
                using (process)
                    CloseProcess(process);
            }

            List<Process> remainingProcesses = GetOtherSleepStrapProcesses(currentProcessId);
            List<int> remainingProcessIds = remainingProcesses.Select(process => process.Id).ToList();
            remainingProcesses.ForEach(process => process.Dispose());

            if (remainingProcessIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"SleepStrap could not close these running processes: {String.Join(", ", remainingProcessIds)}.");
            }

            return processes.Count;
        }

        private static List<Process> GetOtherSleepStrapProcesses(int currentProcessId)
        {
            List<Process> matches = new();
            foreach (Process process in Utilities.GetProcessesSafe())
            {
                if (process.Id != currentProcessId && IsSleepStrapProcess(process))
                    matches.Add(process);
                else
                    process.Dispose();
            }

            return matches;
        }

        private static bool IsSleepStrapProcess(Process process)
        {
            try
            {
                return process.ProcessName.StartsWith("SleepStrap", StringComparison.OrdinalIgnoreCase);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void CloseProcess(Process process)
        {
            try
            {
                if (process.HasExited)
                    return;

                App.Logger.WriteLine(LogIdent, $"Closing SleepStrap process {process.Id} ({process.ProcessName})");
                process.CloseMainWindow();

                if (!process.WaitForExit(1500))
                {
                    process.Kill(true);
                    if (!process.WaitForExit(5000))
                        throw new InvalidOperationException($"SleepStrap process {process.Id} did not close.");
                }
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The process exited between checks.
            }
        }
    }
}
