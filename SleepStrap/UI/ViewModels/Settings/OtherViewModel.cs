namespace SleepStrap.UI.ViewModels.Settings
{
    public class OtherViewModel : NotifyPropertyChangedViewModel
    {
        public string VersionText => $"SleepStrap {new Version(App.Version).ToString(3)}";

        public bool CloseSleepStrapOnLaunch
        {
            get => App.Settings.Prop.CloseSleepStrapOnLaunch;
            set
            {
                if (value == App.Settings.Prop.CloseSleepStrapOnLaunch)
                    return;

                App.Settings.Prop.CloseSleepStrapOnLaunch = value;
                App.Settings.Save();
                OnPropertyChanged(nameof(CloseSleepStrapOnLaunch));
            }
        }
    }
}
