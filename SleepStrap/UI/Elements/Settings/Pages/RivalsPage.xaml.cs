using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class RivalsPage
    {
        public RivalsPage()
        {
            DataContext = new RivalsViewModel();
            InitializeComponent();
        }
    }
}
