using System.IO;
using System.Runtime.InteropServices;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Services;

public sealed class RecorderController
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly IMiniRecorderWindow _window;
    private readonly IAudioCaptureService _audioCapture;
    private readonly ITranscriptionService _transcription;
    private readonly IClipboardPasteService _clipboardPaste;
    private readonly SettingsStore _settingsStore;
    private readonly SoundCueService _soundCues;
    private readonly Func<IntPtr> _getForegroundWindow;
    private readonly DictationQueueProcessor _queue;
    private string? _currentAudioPath;
    private IntPtr _targetWindow;
    private CancellationTokenSource? _runCancellation;

    public RecorderController(
        MiniRecorderViewModel viewModel,
        IMiniRecorderWindow window,
        IAudioCaptureService audioCapture,
        ITranscriptionService transcription,
        IClipboardPasteService clipboardPaste,
        SettingsStore settingsStore,
        SoundCueService? soundCues = null,
        Func<IntPtr>? getForegroundWindow = null)
    {
        _viewModel = viewModel;
        _window = window;
        _audioCapture = audioCapture;
        _transcription = transcription;
        _clipboardPaste = clipboardPaste;
        _settingsStore = settingsStore;
        _soundCues = soundCues ?? new SoundCueService();
        _getForegroundWindow = getForegroundWindow ?? GetForegroundWindow;
        _queue = new DictationQueueProcessor(ProcessJobAsync);
        _queue.PendingCountChanged += count =>
            _ = _window.InvokeAsync(() => _viewModel.PendingTranscriptionCount = count);
        _audioCapture.AudioLevelChanged += level =>
            _ = _window.InvokeAsync(() => _viewModel.AudioLevel = level);
    }

    public async Task ToggleAsync()
    {
        if (_viewModel.State == RecordingState.Recording)
        {
            await StopAndTranscribeAsync();
            return;
        }

        if (_viewModel.State is RecordingState.Busy)
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

        _targetWindow = _getForegroundWindow();
        _runCancellation = new CancellationTokenSource();
        var recordingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Recordings");
        _currentAudioPath = _audioCapture.Start(recordingsRoot);
        _viewModel.State = RecordingState.Recording;
        _soundCues.PlayRecordingStarted();
        _window.ShowBottomCenter();
    }

    public Task StopAndTranscribeAsync()
    {
        if (_currentAudioPath is null)
        {
            return Task.CompletedTask;
        }

        var settings = _settingsStore.Load();
        var cancellationToken = _runCancellation?.Token ?? CancellationToken.None;
        var audioPath = _currentAudioPath;
        var targetWindow = _targetWindow;
        var runCancellation = _runCancellation;

        _audioCapture.Stop();
        _currentAudioPath = null;
        _targetWindow = IntPtr.Zero;
        _runCancellation = null;
        _viewModel.State = RecordingState.Idle;
        _soundCues.PlayTranscribingStarted();
        _ = _queue.Enqueue(new DictationJob(audioPath, targetWindow, settings), cancellationToken)
            .ContinueWith(_ => runCancellation?.Dispose(), TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public Task WaitForPendingTranscriptionsAsync()
    {
        return _queue.WhenIdleAsync();
    }

    private async Task ProcessJobAsync(DictationJob job, CancellationToken cancellationToken)
    {
        try
        {
            var transcript = await _transcription.TranscribeAsync(
                job.Settings.WhisperCliPath,
                job.Settings.ModelPath,
                job.AudioPath,
                job.Settings.WhisperInitialPrompt,
                job.Settings.WhisperThreads,
                cancellationToken);

            if (job.Settings.EnableVocabularyCorrections)
            {
                transcript = VocabularyCorrectionService.ApplyDefaults(transcript);
            }

            if (job.Settings.AppendTrailingSpace)
            {
                transcript = PasteTextFormatter.EnsureTrailingSeparator(transcript);
            }

            await _window.InvokeAsync(() => _viewModel.AddCompletedTranscript(transcript));

            await _window.InvokeAsync(() => _clipboardPaste.PasteAsync(
                    transcript,
                    job.Settings.RestoreClipboardAfterPaste,
                    job.Settings.ClipboardRestoreDelayMs,
                    job.TargetWindow,
                    cancellationToken));

            await _window.InvokeAsync(() =>
            {
                if (!_viewModel.IsRecording && _viewModel.PendingTranscriptionCount <= 1)
                {
                    _window.Hide();
                }
            });
            _soundCues.PlayCompleted();
        }
        catch (Exception error)
        {
            var message = ShortError(error);
            await _window.InvokeAsync(() => _viewModel.SetError(message));
            _soundCues.PlayError();
            LogError(error.ToString());
        }
        finally
        {
            if (!job.Settings.KeepRecordingsForDiagnostics)
            {
                TryDelete(job.AudioPath);
            }
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
