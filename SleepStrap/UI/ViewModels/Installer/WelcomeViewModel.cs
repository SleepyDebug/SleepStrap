namespace SleepStrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            "[SleepBlox on GitHub](https://github.com/SleepyDebug/SleepStrap)"
        );

        public bool CanContinue { get; set; } = false;
    }
}
