using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class MacroPage
    {
        public MacroPage()
        {
            DataContext = new MacroViewModel();
            InitializeComponent();
        }
    }
}
