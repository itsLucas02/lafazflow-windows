using System.IO;
using System.Runtime.InteropServices;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Services;

public sealed class RecorderController
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly MiniRecorderWindow _window;
    private readonly AudioCaptureService _audioCapture;
    private readonly WhisperCliTranscriptionService _transcription;
    private readonly ClipboardPasteService _clipboardPaste;
    private readonly SettingsStore _settingsStore;
    private string? _currentAudioPath;
    private IntPtr _targetWindow;
    private CancellationTokenSource? _runCancellation;

    public RecorderController(
        MiniRecorderViewModel viewModel,
        MiniRecorderWindow window,
        AudioCaptureService audioCapture,
        WhisperCliTranscriptionService transcription,
        ClipboardPasteService clipboardPaste,
        SettingsStore settingsStore)
    {
        _viewModel = viewModel;
        _window = window;
        _audioCapture = audioCapture;
        _transcription = transcription;
        _clipboardPaste = clipboardPaste;
        _settingsStore = settingsStore;
        _audioCapture.AudioLevelChanged += level =>
            _window.Dispatcher.BeginInvoke(() => _viewModel.AudioLevel = level);
    }

    public async Task ToggleAsync()
    {
        if (_viewModel.State == RecordingState.Recording)
        {
            await StopAndTranscribeAsync();
            return;
        }

        if (_viewModel.State is RecordingState.Transcribing or RecordingState.Enhancing or RecordingState.Busy)
        {
            return;
        }

        StartRecording();
    }

    public void StartRecording()
    {
        var settings = _settingsStore.Load();
        var validationError = WhisperCliTranscriptionService.ValidatePaths(settings.WhisperCliPath, settings.ModelPath);
        if (validationError is not null)
        {
            _viewModel.SetError(validationError);
            LogError(validationError);
            _window.ShowBottomCenter();
            return;
        }

        _targetWindow = GetForegroundWindow();
        _runCancellation = new CancellationTokenSource();
        var recordingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Recordings");
        _currentAudioPath = _audioCapture.Start(recordingsRoot);
        _viewModel.State = RecordingState.Recording;
        _window.ShowBottomCenter();
    }

    public async Task StopAndTranscribeAsync()
    {
        if (_currentAudioPath is null)
        {
            return;
        }

        var settings = _settingsStore.Load();
        var cancellationToken = _runCancellation?.Token ?? CancellationToken.None;
        _audioCapture.Stop();
        _viewModel.State = RecordingState.Transcribing;

        try
        {
            var transcript = await _transcription.TranscribeAsync(
                settings.WhisperCliPath,
                settings.ModelPath,
                _currentAudioPath,
                settings.WhisperInitialPrompt,
                settings.WhisperThreads,
                cancellationToken);

            if (settings.EnableVocabularyCorrections)
            {
                transcript = VocabularyCorrectionService.ApplyDefaults(transcript);
            }

            if (settings.AppendTrailingSpace)
            {
                transcript = PasteTextFormatter.EnsureTrailingSeparator(transcript);
            }

            await _clipboardPaste.PasteAsync(
                transcript,
                settings.RestoreClipboardAfterPaste,
                settings.ClipboardRestoreDelayMs,
                _targetWindow,
                cancellationToken);

            _window.Hide();
            _viewModel.State = RecordingState.Idle;
        }
        catch (Exception error)
        {
            var message = ShortError(error);
            _viewModel.SetError(message);
            LogError(error.ToString());
        }
        finally
        {
            if (!settings.KeepRecordingsForDiagnostics)
            {
                TryDelete(_currentAudioPath);
            }

            _currentAudioPath = null;
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static string ShortError(Exception error)
    {
        return string.IsNullOrWhiteSpace(error.Message)
            ? error.GetType().Name
            : error.Message;
    }

    private static void LogError(string message)
    {
        var logRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Logs");
        Directory.CreateDirectory(logRoot);
        File.AppendAllText(
            Path.Combine(logRoot, "lafazflow.log"),
            $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
