using SleepStrap.UI.ViewModels.Settings;
using System.Windows;

namespace SleepStrap.UI.Elements.Settings.Pages
{
    public partial class RivalsPage
    {
        public RivalsPage()
        {
            DataContext = new RivalsViewModel();
            InitializeComponent();
        }

        private void BlurPreview_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            BlurPreviewOverlay.Visibility = Visibility.Visible;
        }

        private void BlurPreviewOverlay_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            BlurPreviewOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
