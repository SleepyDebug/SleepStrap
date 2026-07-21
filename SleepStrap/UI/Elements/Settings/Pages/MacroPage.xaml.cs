using SleepStrap.UI.ViewModels.Settings;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class MacroPage
    {
        private readonly MacroViewModel _viewModel;

        public MacroPage()
        {
            _viewModel = new MacroViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }
    }
}
