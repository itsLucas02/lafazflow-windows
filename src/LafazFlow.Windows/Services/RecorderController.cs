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
    private readonly ITargetTextContextService _targetTextContext;
    private readonly Func<IntPtr> _getForegroundWindow;
    private readonly DictationQueueProcessor _queue;
    private string? _currentAudioPath;
    private LatencyTrace? _currentLatencyTrace;
    private IntPtr _targetWindow;
    private CancellationTokenSource? _runCancellation;
    private Task _stopHandoffTask = Task.CompletedTask;

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
        ILatencyReporter? latencyReporter = null,
        ITargetTextContextService? targetTextContext = null)
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
        _targetTextContext = targetTextContext ?? new FocusedTargetTextContextService();
        _getForegroundWindow = getForegroundWindow ?? GetForegroundWindow;
        _queue = new DictationQueueProcessor(ProcessJobAsync);
        _queue.PendingCountChanged += count =>
            _ = _window.InvokeAsync(() => _viewModel.PendingTranscriptionCount = count);
        _audioCapture.AudioLevelChanged += level =>
            _ = _window.InvokeAsync(() => _viewModel.AudioLevel = level);
        _audioCapture.AudioChunkAvailable += audioChunk =>
            _livePreview.AcceptAudioChunk(audioChunk);
    }

    public async Task ToggleAsync(long? hotkeyTimestamp = null)
    {
        var toggleHandlingTimestamp = Stopwatch.GetTimestamp();
        if (_viewModel.State == RecordingState.Recording)
        {
            await StopAndTranscribeAsync(hotkeyTimestamp, toggleHandlingTimestamp);
            return;
        }

        if (_viewModel.State is RecordingState.Starting
            or RecordingState.Transcribing
            or RecordingState.Enhancing
            or RecordingState.Busy)
        {
            return;
        }

        StartRecording(hotkeyTimestamp, toggleHandlingTimestamp);
    }

    public void StartRecording(long? hotkeyTimestamp = null, long? toggleHandlingTimestamp = null)
    {
        var settings = _settingsStore.Load();
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var validationError = WhisperCliTranscriptionService.ValidatePaths(
            runtime.CliPath,
            runtime.ModelPath,
            runtime.DecodeOptions);
        if (validationError is not null)
        {
            _viewModel.SetError(validationError);
            LogError(validationError);
            _soundCues.PlayError(SoundCueOptions.FromSettings(settings));
            _window.ShowBottomCenter();
            return;
        }

        _targetWindow = _getForegroundWindow();
        _currentLatencyTrace = new LatencyTrace
        {
            ModelPath = runtime.ModelPath,
            Threads = settings.WhisperThreads,
            TargetProcessName = GetProcessName(_targetWindow)
        };
        if (hotkeyTimestamp.HasValue)
        {
            _currentLatencyTrace.Mark(LatencyCheckpoint.HotkeyReceived, hotkeyTimestamp.Value);
        }

        if (toggleHandlingTimestamp.HasValue)
        {
            _currentLatencyTrace.Mark(LatencyCheckpoint.ToggleHandlingStarted, toggleHandlingTimestamp.Value);
        }

        _currentLatencyTrace.Mark(LatencyCheckpoint.RecordingStart);
        _runCancellation = new CancellationTokenSource();
        var recordingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Recordings");
        _currentAudioPath = _audioCapture.Start(recordingsRoot);
        _currentLatencyTrace.Mark(LatencyCheckpoint.RecordingReady);
        _viewModel.State = RecordingState.Recording;
        StartLivePreview(settings, _runCancellation.Token, _currentLatencyTrace);
        _soundCues.PlayRecordingStarted(SoundCueOptions.FromSettings(settings));
        _window.ShowBottomCenter();
        _currentLatencyTrace.Mark(LatencyCheckpoint.RecorderShown);
    }

    public Task StopAndTranscribeAsync(long? hotkeyTimestamp = null, long? toggleHandlingTimestamp = null)
    {
        if (_currentAudioPath is null)
        {
            return Task.CompletedTask;
        }

        var settings = _settingsStore.Load();
        if (hotkeyTimestamp.HasValue)
        {
            _currentLatencyTrace?.Mark(LatencyCheckpoint.StopHotkeyReceived, hotkeyTimestamp.Value);
        }

        if (toggleHandlingTimestamp.HasValue)
        {
            _currentLatencyTrace?.Mark(LatencyCheckpoint.StopRequested, toggleHandlingTimestamp.Value);
        }
        else
        {
            _currentLatencyTrace?.Mark(LatencyCheckpoint.StopRequested);
        }

        var cancellationToken = _runCancellation?.Token ?? CancellationToken.None;
        var audioPath = _currentAudioPath;
        var targetWindow = _targetWindow;
        var latencyTrace = _currentLatencyTrace;
        var runCancellation = _runCancellation;

        _currentAudioPath = null;
        _currentLatencyTrace = null;
        _targetWindow = IntPtr.Zero;
        _runCancellation = null;
        _viewModel.State = RecordingState.Transcribing;
        _soundCues.PlayTranscribingStarted(SoundCueOptions.FromSettings(settings));
        _window.ShowBottomCenter();
        _stopHandoffTask = Task.Run(async () =>
        {
            var queued = false;
            try
            {
                _audioCapture.Stop();
                _ = StopLivePreviewAsync(latencyTrace);
                latencyTrace?.Mark(LatencyCheckpoint.QueueEnqueued);
                _ = _queue.Enqueue(new DictationJob(audioPath, targetWindow, settings, latencyTrace), cancellationToken)
                    .ContinueWith(_ => runCancellation?.Dispose(), TaskScheduler.Default);
                queued = true;
            }
            catch (Exception error)
            {
                var message = ShortError(error);
                await _window.InvokeAsync(() => _viewModel.SetError(message));
                _soundCues.PlayError(SoundCueOptions.FromSettings(settings));
                LogError(error.ToString());
                if (latencyTrace is not null)
                {
                    latencyTrace.Fail(error);
                    _latencyReporter.Report(latencyTrace);
                }

                runCancellation?.Dispose();
            }
            finally
            {
                if (queued)
                {
                    await _window.InvokeAsync(() =>
                    {
                        if (_viewModel.State == RecordingState.Transcribing)
                        {
                            _viewModel.State = RecordingState.Idle;
                        }
                    });
                }
            }
        });
        return Task.CompletedTask;
    }

    private void StartLivePreview(
        AppSettings settings,
        CancellationToken cancellationToken,
        LatencyTrace? latencyTrace)
    {
        if (!settings.ShowLiveTranscriptPreview)
        {
            return;
        }

        try
        {
            latencyTrace?.Mark(LatencyCheckpoint.PreviewStartRequested);
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
            latencyTrace?.Mark(LatencyCheckpoint.PreviewStarted);
        }
        catch (Exception error)
        {
            LogError($"Live preview failed to start: {error}");
        }
    }

    private async Task StopLivePreviewAsync(LatencyTrace? latencyTrace)
    {
        try
        {
            latencyTrace?.Mark(LatencyCheckpoint.PreviewStopRequested);
            await _livePreview.StopAsync();
            latencyTrace?.Mark(LatencyCheckpoint.PreviewStopped);
        }
        catch (Exception error)
        {
            LogError($"Live preview failed to stop: {error}");
        }
    }

    public Task WaitForPendingTranscriptionsAsync()
    {
        return WaitForStopHandoffAndQueueAsync();
    }

    private async Task WaitForStopHandoffAndQueueAsync()
    {
        await _stopHandoffTask;
        await _queue.WhenIdleAsync();
    }

    private async Task ProcessJobAsync(DictationJob job, CancellationToken cancellationToken)
    {
        var succeeded = false;
        Exception? capturedError = null;
        try
        {
            job.LatencyTrace?.Mark(LatencyCheckpoint.WhisperStarted);
            var runtime = WhisperCliTranscriptionService.ResolveRuntime(job.Settings);
            var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(job.Settings);
            var transcript = await _transcription.TranscribeAsync(
                runtime.CliPath,
                runtime.ModelPath,
                job.AudioPath,
                prompt,
                job.Settings.WhisperThreads,
                runtime.DecodeOptions,
                cancellationToken);
            job.LatencyTrace?.Mark(LatencyCheckpoint.WhisperFinished);

            job.LatencyTrace?.Mark(LatencyCheckpoint.PostProcessingStarted);
            if (job.Settings.EnableVocabularyCorrections)
            {
                transcript = VocabularyCorrectionService.Apply(transcript, job.Settings.CustomCorrectionRules);
            }

            var targetContext = _targetTextContext.GetTextBeforeCaret(job.TargetWindow);
            transcript = TextContinuationFormatter.ApplyTargetContext(transcript, targetContext);

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

            job.LatencyTrace?.Mark(LatencyCheckpoint.UiHideStarted);
            await _window.InvokeAsync(() =>
            {
                if (!_viewModel.IsRecording && _viewModel.PendingTranscriptionCount <= 1)
                {
                    _window.Hide();
                    job.LatencyTrace?.Mark(LatencyCheckpoint.UiHidden);
                }
            });
            _soundCues.PlayCompleted(SoundCueOptions.FromSettings(job.Settings));
            succeeded = true;
        }
        catch (Exception error)
        {
            capturedError = error;
            var message = ShortError(error);
            await _window.InvokeAsync(() => _viewModel.SetError(message));
            _soundCues.PlayError(SoundCueOptions.FromSettings(job.Settings));
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
