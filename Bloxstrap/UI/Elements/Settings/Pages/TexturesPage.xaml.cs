using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    public partial class TexturesPage
    {
        public TexturesPage()
        {
            DataContext = new VisualModsViewModel();
            InitializeComponent();
        }
    }
}
