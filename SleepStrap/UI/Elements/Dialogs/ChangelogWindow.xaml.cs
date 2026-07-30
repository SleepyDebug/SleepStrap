using System.Windows;

namespace SleepStrap.UI.Elements.Dialogs
{
    public partial class ChangelogWindow : Window
    {
        public ChangelogWindow(string version)
        {
            InitializeComponent();
            VersionText.Text = $"Version {version}";
        }

        private void OkWhatever_Click(object sender, RoutedEventArgs e) => Close();
    }
}
