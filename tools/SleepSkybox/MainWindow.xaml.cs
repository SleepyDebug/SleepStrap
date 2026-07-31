using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using Forms = System.Windows.Forms;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfMessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SleepSkybox;

public partial class MainWindow : Window
{
    private string? _inputPath;
    private string? _outputPath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ChoosePng_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PNG panorama (*.png)|*.png",
            Multiselect = false,
            Title = "Choose an equirectangular PNG panorama"
        };
        if (dialog.ShowDialog(this) == true)
            SelectInput(dialog.FileName);
    }

    private void ChooseOutput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose where to create the Roblox skybox files",
            UseDescriptionForTitle = true,
            SelectedPath = _outputPath ?? String.Empty
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _outputPath = dialog.SelectedPath;
            UpdateUi();
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(WpfDataFormats.FileDrop)
            ? WpfDragDropEffects.Copy
            : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(WpfDataFormats.FileDrop))
            return;

        string[] files = (string[])e.Data.GetData(WpfDataFormats.FileDrop);
        string? png = files.FirstOrDefault(path =>
            File.Exists(path) && String.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase));
        if (png is null)
        {
            StatusText.Text = "Drop a PNG panorama file.";
            return;
        }

        SelectInput(png);
    }

    private void SelectInput(string inputPath)
    {
        _inputPath = inputPath;
        _outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath)!,
            $"{Path.GetFileNameWithoutExtension(inputPath)} - SleepBlox Sky");
        StatusText.Text = "Ready to build six Roblox sky files.";
        UpdateUi();
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (_inputPath is null || _outputPath is null)
            return;

        string input = _inputPath;
        string output = _outputPath;
        string skyFolder = SkyboxConverter.GetSkyDirectory(output);
        string[] expected = SkyboxConverter.OutputFileNames.Select(name => Path.Combine(skyFolder, name)).ToArray();
        if (expected.Any(File.Exists) &&
            WpfMessageBox.Show(
                this,
                "That output folder already has skybox files. Replace those generated files?",
                "SleepSky",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true, "Building six 512px sky faces…");
        try
        {
            await Task.Run(() => SkyboxConverter.ConvertPngPanorama(input, output));
            StatusText.Text = "Skybox created. Import it from SleepBlox Experimental.";
            OpenOutputButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not create that skybox.";
            WpfMessageBox.Show(this, ex.Message, "SleepSky", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (String.IsNullOrWhiteSpace(_outputPath) || !Directory.Exists(_outputPath))
            return;

        Process.Start(new ProcessStartInfo(_outputPath) { UseShellExecute = true });
    }

    private void SetBusy(bool busy, string? status)
    {
        ConvertButton.IsEnabled = !busy && _inputPath is not null && _outputPath is not null;
        OpenOutputButton.IsEnabled = !busy && _outputPath is not null && Directory.Exists(_outputPath);
        if (status is not null)
            StatusText.Text = status;
    }

    private void UpdateUi()
    {
        InputPathText.Text = _inputPath ?? "Drop a 2:1 PNG here, or choose one.";
        OutputPathText.Text = _outputPath ?? "A new folder will be created beside the PNG.";
        ConvertButton.IsEnabled = _inputPath is not null && _outputPath is not null;
        OpenOutputButton.IsEnabled = _outputPath is not null && Directory.Exists(_outputPath);
    }
}
