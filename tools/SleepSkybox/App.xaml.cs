using System.Windows;

namespace SleepSkybox;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Optional script mode: SleepSkybox.exe "panorama.png" "output folder"
        // keeps this useful from a shortcut or batch file without ever touching Roblox.
        if (e.Args.Length == 2)
        {
            try
            {
                SkyboxConverter.ConvertPngPanorama(e.Args[0], e.Args[1]);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    "SleepSky",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }

            return;
        }

        base.OnStartup(e);
    }
}
