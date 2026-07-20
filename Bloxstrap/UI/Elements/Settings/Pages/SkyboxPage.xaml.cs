using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
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
