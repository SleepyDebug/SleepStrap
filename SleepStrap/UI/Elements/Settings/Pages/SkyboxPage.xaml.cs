using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class SkyboxPage
    {
        public SkyboxPage()
        {
            DataContext = new SkyboxViewModel();
            InitializeComponent();
        }
    }
}
