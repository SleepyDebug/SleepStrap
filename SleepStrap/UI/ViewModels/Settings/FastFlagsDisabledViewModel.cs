using System.Windows.Controls;

namespace SleepStrap.UI.ViewModels.Settings
{
    public class FastFlagsDisabledViewModel : NotifyPropertyChangedViewModel
    {
        private Page _page;

        public FastFlagsDisabledViewModel(Page page) => _page = page;
    }
}