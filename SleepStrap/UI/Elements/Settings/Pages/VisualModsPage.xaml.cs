using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class VisualModsPage
    {
        public VisualModsPage()
        {
            DataContext = new VisualModsViewModel();
            InitializeComponent();
        }
    }
}
