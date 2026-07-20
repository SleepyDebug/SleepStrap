using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
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
