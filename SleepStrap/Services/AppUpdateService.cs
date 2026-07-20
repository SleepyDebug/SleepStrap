using System.Windows;

namespace SleepStrap.Services
{
    internal static class AppUpdateService
    {
        private const string LogIdent = "AppUpdateService";
        private static int _hasChecked;
        private static InterProcessLock? _updateLock;

        public static async Task<bool> CheckAndPromptAsync()
        {
            if (Interlocked.Exchange(ref _hasChecked, 1) != 0)
                return false;

            if (!App.SupportsSelfUpdates ||
                !App.Settings.Prop.CheckForUpdates ||
                App.LaunchSettings.BypassUpdateCheck ||
                App.LaunchSettings.UpgradeFlag.Active ||
                App.LaunchSettings.BackgroundUpdaterFlag.Active ||
                App.LaunchSettings.MultiInstanceWatcherFlag.Active)
            {
                return false;
            }

            try
            {
                App.Logger.WriteLine(LogIdent, "Checking GitHub for a newer SleepStrap release");
                GithubRelease? release = await App.GetLatestRelease();
                if (release?.Assets is null)
                    return false;

                Version installedVersion = Utilities.GetVersionFromString(App.Version);
                Version releaseVersion = Utilities.GetVersionFromString(release.TagName);
                if (installedVersion >= releaseVersion)
                {
                    App.Logger.WriteLine(LogIdent, $"SleepStrap {installedVersion} is current (latest: {releaseVersion})");
                    return false;
                }

                GithubReleaseAsset? asset = release.Assets
                    .Where(item => item.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.Name.StartsWith("SleepStrap", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                if (asset is null)
                {
                    App.Logger.WriteLine(LogIdent, $"Release '{release.TagName}' has no Windows executable asset");
                    return false;
                }

                MessageBoxResult answer = Frontend.ShowMessageBox(
                    $"SleepStrap {releaseVersion} is available on GitHub. Update now?",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo,
                    MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                {
                    App.Logger.WriteLine(LogIdent, $"Update to {releaseVersion} was declined");
                    return false;
                }

                if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? downloadUri) ||
                    !downloadUri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The GitHub release contains an invalid download URL.");
                }

                Directory.CreateDirectory(Paths.TempUpdates);
                string assetName = Path.GetFileName(asset.Name);
                string downloadLocation = Path.Combine(Paths.TempUpdates, assetName);

                App.Logger.WriteLine(LogIdent, $"Downloading {release.TagName} from GitHub");
                using (HttpResponseMessage response = await App.HttpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    await using Stream input = await response.Content.ReadAsStreamAsync();
                    await using FileStream output = new(downloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
                    await input.CopyToAsync(output);
                }

                await ValidateDownloadedExecutableAsync(downloadLocation);

                _updateLock = new InterProcessLock("AutoUpdater");
                if (!_updateLock.IsAcquired)
                    throw new InvalidOperationException("Another SleepStrap update is already running.");

                ProcessStartInfo startInfo = new()
                {
                    FileName = downloadLocation,
                    UseShellExecute = true
                };
                startInfo.ArgumentList.Add("-upgrade");
                foreach (string arg in App.LaunchSettings.Args)
                {
                    if (!String.Equals(arg, "-upgrade", StringComparison.OrdinalIgnoreCase))
                        startInfo.ArgumentList.Add(arg);
                }

                App.Settings.Save();
                Process.Start(startInfo);
                App.Logger.WriteLine(LogIdent, $"Started SleepStrap {releaseVersion} updater");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LogIdent, ex);
                Frontend.ShowMessageBox(
                    $"SleepStrap could not check for or install the GitHub update.\n\n{ex.Message}",
                    MessageBoxImage.Information);
                return false;
            }
        }

        private static async Task ValidateDownloadedExecutableAsync(string path)
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length < 1024 * 1024)
                throw new InvalidDataException("The downloaded update is incomplete.");

            byte[] signature = new byte[2];
            await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await input.ReadAsync(signature) != signature.Length || signature[0] != (byte)'M' || signature[1] != (byte)'Z')
                throw new InvalidDataException("The downloaded update is not a Windows executable.");
        }
    }
}
