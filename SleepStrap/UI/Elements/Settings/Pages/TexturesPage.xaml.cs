using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
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
