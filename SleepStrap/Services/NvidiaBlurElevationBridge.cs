using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace SleepStrap.Services
{
    internal static class NvidiaBlurElevationBridge
    {
        private const string HelperArgument = "--sleepstrap-nvidia-blur-helper";

        private sealed class HelperJob
        {
            public bool Enable { get; set; }
            public Dictionary<string, string> Backup { get; set; } = new();
            public string ResultPath { get; set; } = String.Empty;
        }

        internal sealed class HelperResult
        {
            public bool Success { get; set; }
            public Dictionary<string, string> Backup { get; set; } = new();
            public string Error { get; set; } = String.Empty;
        }

        public static bool TryHandleElevatedHelper(string[] arguments)
        {
            if (arguments.Length != 2 ||
                !String.Equals(arguments[0], HelperArgument, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            HelperResult result;
            string jobPath = arguments[1];
            try
            {
                HelperJob job = JsonSerializer.Deserialize<HelperJob>(File.ReadAllText(jobPath))
                    ?? throw new InvalidOperationException("The NVIDIA helper job was empty.");

                Dictionary<string, string> backup;
                if (job.Enable)
                {
                    backup = NvidiaProfileBlurService.Enable();
                }
                else
                {
                    NvidiaProfileBlurService.Disable(job.Backup);
                    backup = new Dictionary<string, string>();
                }

                result = new HelperResult { Success = true, Backup = backup };
                File.WriteAllText(job.ResultPath, JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                result = new HelperResult { Success = false, Error = ex.Message };
                try
                {
                    HelperJob? job = JsonSerializer.Deserialize<HelperJob>(File.ReadAllText(jobPath));
                    if (job is not null && !String.IsNullOrWhiteSpace(job.ResultPath))
                        File.WriteAllText(job.ResultPath, JsonSerializer.Serialize(result));
                }
                catch
                {
                    // The parent process will report a missing result if the job itself was unreadable.
                }
            }

            return true;
        }

        public static async Task<HelperResult> RunElevatedAsync(
            bool enable,
            IReadOnlyDictionary<string, string> backup)
        {
            Directory.CreateDirectory(Paths.Temp);
            string id = Guid.NewGuid().ToString("N");
            string jobPath = Path.Combine(Paths.Temp, $"NvidiaBlur-{id}.job.json");
            string resultPath = Path.Combine(Paths.Temp, $"NvidiaBlur-{id}.result.json");

            var job = new HelperJob
            {
                Enable = enable,
                Backup = backup.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                ResultPath = resultPath
            };

            try
            {
                File.WriteAllText(jobPath, JsonSerializer.Serialize(job));
                var startInfo = new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = $"{HelperArgument} \"{jobPath.Replace("\"", "\\\"")}\"",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using Process process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Windows could not start the NVIDIA profile helper.");
                await process.WaitForExitAsync();

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException(
                        "The NVIDIA profile helper did not return a result.");

                return JsonSerializer.Deserialize<HelperResult>(File.ReadAllText(resultPath))
                    ?? new HelperResult { Error = "The NVIDIA profile helper returned invalid data." };
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new HelperResult
                {
                    Error = "The administrator prompt was cancelled, so the NVIDIA profile was not changed."
                };
            }
            finally
            {
                TryDelete(jobPath);
                TryDelete(resultPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Temporary helper files are harmless and can be cleaned on the next run.
            }
        }
    }
}
