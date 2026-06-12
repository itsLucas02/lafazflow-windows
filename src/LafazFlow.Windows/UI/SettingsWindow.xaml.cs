using System.Diagnostics;
using System.IO;
using System.Windows;
using LafazFlow.Windows.Services;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LafazFlow.Windows.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly SoundCueService _soundCues;

    public SettingsWindow(SettingsViewModel viewModel)
        : this(viewModel, new SoundCueService())
    {
    }

    public SettingsWindow(SettingsViewModel viewModel, SoundCueService soundCues)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _soundCues = soundCues;
        DataContext = viewModel;
    }

    private void BrowseWhisperCli_OnClick(object sender, RoutedEventArgs e)
    {
        BrowseExecutable("Select whisper-cli.exe", path => _viewModel.WhisperCliPath = path);
    }

    private void BrowseCudaWhisperCli_OnClick(object sender, RoutedEventArgs e)
    {
        BrowseExecutable("Select CUDA whisper-cli.exe", path => _viewModel.CudaWhisperCliPath = path);
    }

    private void BrowseModel_OnClick(object sender, RoutedEventArgs e)
    {
        BrowseModel("Select Whisper model", path => _viewModel.ModelPath = path);
    }

    private void BrowseQualityModel_OnClick(object sender, RoutedEventArgs e)
    {
        BrowseModel("Select quality Whisper model", path => _viewModel.QualityModelPath = path);
    }

    private void BrowseVadModel_OnClick(object sender, RoutedEventArgs e)
    {
        BrowseModel("Select VAD model", path => _viewModel.VadModelPath = path);
    }

    private void ImportLocalModel_OnClick(object sender, RoutedEventArgs e)
    {
        BrowseModel("Import local Whisper model", _viewModel.ImportModel);
    }

    private void BrowseExecutable(string title, Action<string> applyPath)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = title,
            Filter = "whisper-cli.exe|whisper-cli.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            applyPath(dialog.FileName);
        }
    }

    private void BrowseModel(string title, Action<string> applyPath)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = title,
            Filter = "Whisper models (*.bin)|*.bin|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            applyPath(dialog.FileName);
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

    private void OpenModelDirectory_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_viewModel.ModelDirectory);
    }

    private async void PrimaryModelAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ModelCardViewModel card })
        {
            return;
        }

        if (card.CanDownload)
        {
            await _viewModel.DownloadModelAsync(card, CancellationToken.None);
            return;
        }

        _viewModel.UseModel(card);
    }

    private void DeleteModel_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ModelCardViewModel card })
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            $"Delete {card.DisplayName} from the local model folder?",
            "Delete Model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteModel(card);
        }
    }

    private void OpenModelFolder_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ModelCardViewModel card })
        {
            OpenFolder(Path.GetDirectoryName(card.InstallPath) ?? _viewModel.ModelDirectory);
        }
    }

    private void RefreshRuntimeDiagnostics_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshRuntimeDiagnostics();
    }

    private void TestMicrophone_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.TestMicrophone();
    }

    private async void TestTranscription_OnClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.TestTranscriptionSmokeAsync(CancellationToken.None);
    }

    private void ResetSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            this,
            "Reset LafazFlow settings to detected defaults?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ResetSettingsToDefaults();
        }
    }

    private void RefreshLatency_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshLatencyDiagnostics();
    }

    private void ClearLatency_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearLatencyDiagnostics();
    }

    private void TestStartSoundCue_OnClick(object sender, RoutedEventArgs e)
    {
        _soundCues.PlayRecordingStarted(BuildEditedSoundCueOptions());
    }

    private void TestStopSoundCue_OnClick(object sender, RoutedEventArgs e)
    {
        _soundCues.PlayTranscribingStarted(BuildEditedSoundCueOptions());
    }

    private void TestDoneSoundCue_OnClick(object sender, RoutedEventArgs e)
    {
        _soundCues.PlayCompleted(BuildEditedSoundCueOptions());
    }

    private void TestErrorSoundCue_OnClick(object sender, RoutedEventArgs e)
    {
        _soundCues.PlayError(BuildEditedSoundCueOptions());
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

    private SoundCueOptions BuildEditedSoundCueOptions()
    {
        return new SoundCueOptions(
            _viewModel.EnableSoundCues,
            (float)Math.Clamp(_viewModel.SoundCueVolumePercent / 100.0, 0, 1),
            (float)Math.Clamp(_viewModel.SoundCueRecordingStartedVolumePercent / 100.0, 0, 2),
            (float)Math.Clamp(_viewModel.SoundCueTranscribingStartedVolumePercent / 100.0, 0, 2),
            (float)Math.Clamp(_viewModel.SoundCueCompletedVolumePercent / 100.0, 0, 2),
            (float)Math.Clamp(_viewModel.SoundCueErrorVolumePercent / 100.0, 0, 2));
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
