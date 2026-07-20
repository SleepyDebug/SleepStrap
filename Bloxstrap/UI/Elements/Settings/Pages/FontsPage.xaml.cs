using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class FontsPage
    {
        public FontsPage()
        {
            DataContext = new VisualModsViewModel();
            InitializeComponent();
        }
    }
}
