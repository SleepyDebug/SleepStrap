using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
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
