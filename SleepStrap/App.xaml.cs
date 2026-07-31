using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;

using Microsoft.Win32;

namespace SleepStrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "SleepBlox-QA";
#else
        public const string ProjectName = "SleepBlox";
#endif
        // Keep the old identity only for migration. Existing installations continue
        // to use their original folder and settings after the public rebrand.
        public const string LegacyProjectName = "SleepStrap";
        public const string ProjectOwner = "SleepyDebug";
        public const string ProjectRepository = "SleepyDebug/SleepStrap";
        public const string ProjectDownloadLink = "https://github.com/SleepyDebug/SleepStrap/releases";
        public const string ProjectHelpLink = "https://github.com/SleepyDebug/SleepStrap#readme";
        public const string ProjectSupportLink = "https://github.com/SleepyDebug/SleepStrap/issues/new";
        public const string ProjectRemoteDataLink = "https://config.fishstrap.app/v1/Data.json";
        public const bool SupportsSelfUpdates = true;

        public static string BootstrapperMutexName => $"{ProjectName}-Bootstrapper";
        public static string BackgroundUpdaterMutexName => $"{ProjectName}-BackgroundUpdater";
        public static string BackgroundUpdaterKillEventName => $"{ProjectName}-BackgroundUpdaterKillEvent";
        public static string MultiInstanceWatcherEventName => $"{ProjectName}-MultiInstanceWatcherInitialisationFinished";

        public const string LegacyBootstrapperMutexName = "SleepStrap-Bootstrapper";
        public const string LegacyBackgroundUpdaterMutexName = "SleepStrap-BackgroundUpdater";
        public const string LegacyBackgroundUpdaterKillEventName = "SleepStrap-BackgroundUpdaterKillEvent";
        public const string LegacyMultiInstanceWatcherEventName = "SleepStrap-MultiInstanceWatcherInitialisationFinished";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta.exe";
        public const string RobloxStudioAppName = "RobloxStudioBeta.exe";
        // one day ill add studio support, haha i never did!
        public const string RobloxAnselAppName = "eurotrucks2.exe";

        // simple shorthand for extremely frequently used and long string - this goes under HKCU
        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";
        public const string LegacyUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SleepStrap";

        public const string ApisKey = $"Software\\{ProjectName}";

        public static LaunchSettings LaunchSettings { get; private set; } = null!;

        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        public static Bootstrapper? Bootstrapper { get; set; } = null!;

        public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static bool IsStudioVisible => !String.IsNullOrEmpty(App.RobloxState.Prop.Studio.VersionGuid);

        public static readonly MD5 MD5Provider = MD5.Create();

        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        public static readonly JsonManager<Settings> Settings = new();

        public static readonly JsonManager<State> State = new();

        public static readonly JsonManager<RobloxState> RobloxState = new();

        public static readonly RemoteDataManager RemoteData = new();

        public static readonly FastFlagManager FastFlags = new();

        public static readonly GBSEditor GlobalSettings = new();

        public static readonly HttpClient HttpClient = new(
            new HttpClientLoggingHandler(
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            )
        );

        private static bool _showingExceptionDialog = false;

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Current.Dispatcher.Invoke(() => Current.Shutdown(exitCodeNum));
        }

        void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

            FinalizeExceptionHandling(e.Exception);
        }

        public static void FinalizeExceptionHandling(AggregateException ex)
        {
            foreach (var innerEx in ex.InnerExceptions)
                Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

            FinalizeExceptionHandling(ex.GetBaseException(), false);
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log)
                Logger.WriteException("App::FinalizeExceptionHandling", ex);

            if (_showingExceptionDialog)
                return;

            _showingExceptionDialog = true;

            SendLog();

            if (Bootstrapper?.Dialog != null)
            {
                if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                    Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static async Task<GithubRelease?> GetLatestRelease()
        {
            const string LOG_IDENT = "App::GetLatestRelease";

            try
            {
                var releaseInfo = await Http.GetJson<GithubRelease>($"https://api.github.com/repos/{ProjectRepository}/releases/latest");

                if (releaseInfo is null || releaseInfo.Assets is null)
                {
                    Logger.WriteLine(LOG_IDENT, "Encountered invalid data");
                    return null;
                }

                return releaseInfo;
            }
            catch (Exception ex)
            {
                Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public static void SendLog()
        {

        }

        public static void AssertWindowsOSVersion()
        {
            const string LOG_IDENT = "App::AssertWindowsOSVersion";

            int major = Environment.OSVersion.Version.Major;
            if (major < 10) // Windows 10 and newer only
            {
                Logger.WriteLine(LOG_IDENT, $"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

                if (!LaunchSettings.QuietFlag.Active)
                    Frontend.ShowMessageBox(Strings.App_OSDeprecation_Win7_81, MessageBoxImage.Error);

                Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";

            if (Services.NvidiaBlurElevationBridge.TryHandleElevatedHelper(e.Args))
            {
                Shutdown();
                return;
            }

            Locale.Initialize();

            base.OnStartup(e);

            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            string userAgent = $"{ProjectName}/{Version}";

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent += $" (Production)";
                else
                    userAgent += $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})";
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent += " (QA)";
#else
                userAgent += $" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})";
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");
            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            LaunchSettings = new LaunchSettings(e.Args);

            // installation check begins here
            // A rebranded build must still find the existing SleepStrap install so
            // it keeps the user's configuration, Roblox registration, and local mods.
            RegistryKey? currentUninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
#if !QA_BUILD
            RegistryKey? legacyUninstallKey = currentUninstallKey is null
                ? Registry.CurrentUser.OpenSubKey(LegacyUninstallKey)
                : null;
            bool rebrandingLegacyInstall = legacyUninstallKey is not null;
            using RegistryKey? uninstallKey = currentUninstallKey ?? legacyUninstallKey;
#else
            const bool rebrandingLegacyInstall = false;
            using RegistryKey? uninstallKey = currentUninstallKey;
#endif
            string? installLocation = null;
            bool fixInstallLocation = false;

            if (uninstallKey?.GetValue("InstallLocation") is string value)
            {
                if (Directory.Exists(value))
                {
                    installLocation = value;
                }
                else
                {
                    // check if user profile folder has been renamed
                    var match = Regex.Match(value, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string newLocation = value.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);

                        if (Directory.Exists(newLocation))
                        {
                            installLocation = newLocation;
                            fixInstallLocation = true;
                        }
                    }
                }
            }

            // silently change install location if we detect a portable run
            if (installLocation is null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(x => Path.GetFileName(x)).ToArray();

                // check if settings.json and state.json are the only files in the folder
                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation is not null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {
                    // force reinstall
                    installLocation = null;
                }
            }

            if (installLocation is null)
            {
                Logger.Initialize(true);
                AssertWindowsOSVersion();
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");
                AssertWindowsOSVersion(); // prevent new installs from unsupported operating systems
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);

                // ensure executable is in the install directory
                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                    File.Copy(Paths.Process, Paths.Application);

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                Settings.Load();
                State.Load();
                RobloxState.Load();
                FastFlags.Load();
                GlobalSettings.Load();

                // Older builds hard-disabled this setting. Migrate those
                // installations now that releases are published in the current repo.
                if (!Settings.Prop.CheckForUpdates)
                {
                    Settings.Prop.CheckForUpdates = true;
                    Settings.Save();
                }

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Locale.Set(Settings.Prop.Locale);

                // Finish the public rebrand on existing installations. Without this,
                // browser protocols and shortcuts can keep launching an old-named
                // executable even after a user has installed SleepBlox.
                if (rebrandingLegacyInstall)
                    Installer.MigrateLegacyInstallation();

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                bool isBackgroundHost = LaunchSettings.ClippingFlag.Active || LaunchSettings.ExperimentalFlag.Active;

                if (!isBackgroundHost && await Services.AppUpdateService.CheckAndPromptAsync())
                {
                    SoftTerminate();
                    return;
                }

                _ = Task.Run(App.RemoteData.LoadData); // ok

                WindowsRegistry.RegisterApis(); // we want to register those early on
                                                // so we wont have any issues with bloxshade

                if (!isBackgroundHost)
                    Services.ClippingHostService.StopRemovedSleePlayer();

                if (!isBackgroundHost && Settings.Prop.ClippingEnabled)
                    Services.ClippingHostService.EnsureStarted();

                if (!isBackgroundHost)
                    Services.ExperimentalClickerHostService.EnsureStarted();

                LaunchHandler.ProcessLaunchArgs();
            }

            // you must *explicitly* call terminate when everything is done, it won't be called implicitly
        }
    }
}
