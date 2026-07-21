using System.Collections.ObjectModel;

namespace SleepStrap.Models.Persistable
{
    public class Settings
    {
        // SleepStrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentAeroDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconSleepStrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Dark;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        public bool ForceLocalData { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool MultiInstanceLaunching { get; set; } = false;
        public bool ConfirmLaunches { get; set; } = true;
        public string Locale { get; set; } = "nil";
        public bool ForceRobloxLanguage { get; set; } = false;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = false;
        public bool UpdateRoblox { get; set; } = true;
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public ChannelChangeMode ChannelChangeMode { get; set; } = ChannelChangeMode.Automatic;
        public string ChannelHash { get; set; } = "";
        public string DownloadingStringFormat { get; set; } = Strings.Bootstrapper_Status_Downloading + " {0} - {1}MB / {2}MB";
        public string? SelectedCustomTheme { get; set; } = null;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool CloseSleepStrapOnLaunch { get; set; } = false;
        public bool OverrideLegacyBloxstrapSettings { get; set; } = false;
        public Dictionary<string, string> LegacyBloxstrapFlagBackup { get; set; } = new();
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        // integration configuration
        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.Never;
        public List<string> CleanerDirectories { get; set; } = new List<string>();
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public bool ShowServerUptime { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;

        // SleepStrap visual mods
        public bool DarkTexturesEnabled { get; set; } = false;
        public bool BlurryTexturesEnabled { get; set; } = false;
        public Dictionary<string, string> BlurryTexturesFlagBackup { get; set; } = new();
        public bool RtxShineEnabled { get; set; } = false;
        public Dictionary<string, string> RtxShineFlagBackup { get; set; } = new();
        public string SelectedFontName { get; set; } = "Roblox Default";
        public string SelectedFontSource { get; set; } = "";
        public bool CustomSkyboxEnabled { get; set; } = false;
        public string CustomSkyboxSourceName { get; set; } = "";

        // SleepStrap Rivals display stretch
        public bool RivalsStretchEnabled { get; set; } = false;
        public int RivalsStretchPercent { get; set; } = 75;
        public int RivalsNativeWidth { get; set; } = 0;
        public int RivalsNativeHeight { get; set; } = 0;
        public int RivalsNativeFrequency { get; set; } = 0;
        public int RivalsFpsLimit { get; set; } = 0;
        public Dictionary<string, string> RivalsFpsFlagBackup { get; set; } = new();
        public bool RivalsFpsCounterEnabled { get; set; } = false;
        public Dictionary<string, string> RivalsFpsCounterFlagBackup { get; set; } = new();

        // SleepStrap replay buffer
        public bool ClippingEnabled { get; set; } = false;
        public int ClippingBufferMinutes { get; set; } = 3;
        public int ClippingHotkeyModifiers { get; set; } = 1; // MOD_ALT
        public int ClippingHotkeyVirtualKey { get; set; } = 0x58; // X
        public string ClippingDisplayDevice { get; set; } = "";
        public bool ClippingSpeakerEnabled { get; set; } = true;
        public string ClippingSpeakerDevice { get; set; } = "";
        public int ClippingSpeakerVolume { get; set; } = 80;
        public bool ClippingMicrophoneEnabled { get; set; } = false;
        public string ClippingMicrophoneDevice { get; set; } = "";
        public int ClippingMicrophoneVolume { get; set; } = 70;

        // SleepStrap RIVALS loadout macro
        public List<string> MacroMissingWeapons { get; set; } = new();
        public string MacroPrimaryWeapon { get; set; } = "Distortion";
        public string MacroSecondaryWeapon { get; set; } = "Warper";
        public string MacroMeleeWeapon { get; set; } = "Maul";
        public string MacroUtilityWeapon { get; set; } = "Grappler";
        public bool MacroQuickRespawn { get; set; } = false;
        public bool MacroAutoUtility { get; set; } = false;
        public bool MacroAutoInspect { get; set; } = false;
        public bool MacroAutoRejoinHourly { get; set; } = false;
    }
}
