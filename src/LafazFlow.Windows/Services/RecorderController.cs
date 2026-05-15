using System.IO;
using System.Diagnostics;
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
    private readonly ILiveTranscriptPreviewService _livePreview;
    private readonly ILatencyReporter _latencyReporter;
    private readonly Func<IntPtr> _getForegroundWindow;
    private readonly DictationQueueProcessor _queue;
    private string? _currentAudioPath;
    private LatencyTrace? _currentLatencyTrace;
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
        Func<IntPtr>? getForegroundWindow = null,
        ILiveTranscriptPreviewService? livePreview = null,
        ILatencyReporter? latencyReporter = null)
    {
        _viewModel = viewModel;
        _window = window;
        _audioCapture = audioCapture;
        _transcription = transcription;
        _clipboardPaste = clipboardPaste;
        _settingsStore = settingsStore;
        _soundCues = soundCues ?? new SoundCueService();
        _livePreview = livePreview ?? new NullLiveTranscriptPreviewService();
        _latencyReporter = latencyReporter ?? new FileLatencyReporter();
        _getForegroundWindow = getForegroundWindow ?? GetForegroundWindow;
        _queue = new DictationQueueProcessor(ProcessJobAsync);
        _queue.PendingCountChanged += count =>
            _ = _window.InvokeAsync(() => _viewModel.PendingTranscriptionCount = count);
        _audioCapture.AudioLevelChanged += level =>
            _ = _window.InvokeAsync(() => _viewModel.AudioLevel = level);
        _audioCapture.AudioChunkAvailable += audioChunk =>
            _livePreview.AcceptAudioChunk(audioChunk);
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
        _currentLatencyTrace = new LatencyTrace
        {
            ModelPath = settings.ModelPath,
            Threads = settings.WhisperThreads,
            TargetProcessName = GetProcessName(_targetWindow)
        };
        _currentLatencyTrace.Mark(LatencyCheckpoint.RecordingStart);
        _runCancellation = new CancellationTokenSource();
        var recordingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Recordings");
        _currentAudioPath = _audioCapture.Start(recordingsRoot);
        _currentLatencyTrace.Mark(LatencyCheckpoint.RecordingReady);
        _viewModel.State = RecordingState.Recording;
        StartLivePreview(settings, _runCancellation.Token);
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
        _currentLatencyTrace?.Mark(LatencyCheckpoint.StopRequested);
        var cancellationToken = _runCancellation?.Token ?? CancellationToken.None;
        var audioPath = _currentAudioPath;
        var targetWindow = _targetWindow;
        var latencyTrace = _currentLatencyTrace;
        var runCancellation = _runCancellation;

        _audioCapture.Stop();
        _ = StopLivePreviewAsync();
        _currentAudioPath = null;
        _currentLatencyTrace = null;
        _targetWindow = IntPtr.Zero;
        _runCancellation = null;
        _viewModel.State = RecordingState.Idle;
        _soundCues.PlayTranscribingStarted();
        latencyTrace?.Mark(LatencyCheckpoint.QueueEnqueued);
        _ = _queue.Enqueue(new DictationJob(audioPath, targetWindow, settings, latencyTrace), cancellationToken)
            .ContinueWith(_ => runCancellation?.Dispose(), TaskScheduler.Default);
        return Task.CompletedTask;
    }

    private void StartLivePreview(AppSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.ShowLiveTranscriptPreview)
        {
            return;
        }

        try
        {
            _livePreview.StartAsync(
                settings,
                partialTranscript =>
                    _ = _window.InvokeAsync(() =>
                    {
                        if (_viewModel.IsRecording)
                        {
                            _viewModel.PartialTranscript = partialTranscript;
                        }
                    }),
                cancellationToken).GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            LogError($"Live preview failed to start: {error}");
        }
    }

    private async Task StopLivePreviewAsync()
    {
        try
        {
            await _livePreview.StopAsync();
        }
        catch (Exception error)
        {
            LogError($"Live preview failed to stop: {error}");
        }
    }

    public Task WaitForPendingTranscriptionsAsync()
    {
        return _queue.WhenIdleAsync();
    }

    private async Task ProcessJobAsync(DictationJob job, CancellationToken cancellationToken)
    {
        var succeeded = false;
        Exception? capturedError = null;
        try
        {
            job.LatencyTrace?.Mark(LatencyCheckpoint.WhisperStarted);
            var transcript = await _transcription.TranscribeAsync(
                job.Settings.WhisperCliPath,
                job.Settings.ModelPath,
                job.AudioPath,
                job.Settings.WhisperInitialPrompt,
                job.Settings.WhisperThreads,
                cancellationToken);
            job.LatencyTrace?.Mark(LatencyCheckpoint.WhisperFinished);

            job.LatencyTrace?.Mark(LatencyCheckpoint.PostProcessingStarted);
            if (job.Settings.EnableVocabularyCorrections)
            {
                transcript = VocabularyCorrectionService.ApplyDefaults(transcript);
            }

            if (job.Settings.AppendTrailingSpace)
            {
                transcript = PasteTextFormatter.EnsureTrailingSeparator(transcript);
            }
            job.LatencyTrace?.Mark(LatencyCheckpoint.PostProcessingFinished);

            job.LatencyTrace?.Mark(LatencyCheckpoint.UiUpdateStarted);
            await _window.InvokeAsync(() => _viewModel.AddCompletedTranscript(transcript));
            job.LatencyTrace?.Mark(LatencyCheckpoint.UiUpdateFinished);

            job.LatencyTrace?.Mark(LatencyCheckpoint.PasteStarted);
            await _window.InvokeAsync(() => _clipboardPaste.PasteAsync(
                    transcript,
                    job.Settings.RestoreClipboardAfterPaste,
                    job.Settings.ClipboardRestoreDelayMs,
                    job.TargetWindow,
                    cancellationToken));
            job.LatencyTrace?.Mark(LatencyCheckpoint.PasteFinished);

            await _window.InvokeAsync(() =>
            {
                if (!_viewModel.IsRecording && _viewModel.PendingTranscriptionCount <= 1)
                {
                    _window.Hide();
                }
            });
            _soundCues.PlayCompleted();
            succeeded = true;
        }
        catch (Exception error)
        {
            capturedError = error;
            var message = ShortError(error);
            await _window.InvokeAsync(() => _viewModel.SetError(message));
            _soundCues.PlayError();
            LogError(error.ToString());
        }
        finally
        {
            job.LatencyTrace?.Mark(LatencyCheckpoint.CleanupStarted);
            if (!job.Settings.KeepRecordingsForDiagnostics)
            {
                TryDelete(job.AudioPath);
            }
            job.LatencyTrace?.Mark(LatencyCheckpoint.CleanupFinished);
            if (job.LatencyTrace is not null)
            {
                if (succeeded)
                {
                    job.LatencyTrace.Complete();
                }
                else
                {
                    job.LatencyTrace.Fail(capturedError ?? new InvalidOperationException("Dictation failed."));
                }

                _latencyReporter.Report(job.LatencyTrace);
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

    private static string? GetProcessName(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
