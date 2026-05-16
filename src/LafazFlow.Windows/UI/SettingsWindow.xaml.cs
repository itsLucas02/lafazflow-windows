using System.Diagnostics;
using System.IO;
using System.Windows;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LafazFlow.Windows.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void BrowseWhisperCli_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Select whisper-cli.exe",
            Filter = "whisper-cli.exe|whisper-cli.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.WhisperCliPath = dialog.FileName;
        }
    }

    private void BrowseModel_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Select Whisper model",
            Filter = "Whisper models (*.bin)|*.bin|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ModelPath = dialog.FileName;
        }
    }

    private void OpenSettingsFolder_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_viewModel.SettingsFolder);
    }

    private void OpenLogsFolder_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_viewModel.LogsFolder);
    }

    private void OpenRecordingsFolder_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_viewModel.RecordingsFolder);
    }

    private void RefreshLatency_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshLatencyDiagnostics();
    }

    private void ClearLatency_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearLatencyDiagnostics();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var result = _viewModel.Save();
        if (result.Success)
        {
            Close();
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = Quote(path),
            UseShellExecute = true
        });
    }

    private static string Quote(string path)
    {
        return $"\"{path.Replace("\"", "\\\"")}\"";
    }
}
