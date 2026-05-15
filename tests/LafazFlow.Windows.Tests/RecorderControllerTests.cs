using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class RecorderControllerTests
{
    [Fact]
    public async Task StopEnqueuesJobAndAllowsImmediateNextRecording()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav", "second.wav");
        var transcriptionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTranscription = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcription = new FakeTranscriptionService(async audioPath =>
        {
            transcriptionStarted.SetResult();
            await releaseTranscription.Task;
            return $"{audioPath} transcript";
        });
        var paste = new FakeClipboardPasteService();
        var store = CreateSettingsStore();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            transcription,
            paste,
            store,
            new SoundCueService(),
            () => (IntPtr)123);

        controller.StartRecording();
        await controller.ToggleAsync();
        await transcriptionStarted.Task;

        Assert.Equal(RecordingState.Idle, viewModel.State);
        Assert.Equal(1, viewModel.PendingTranscriptionCount);

        await controller.ToggleAsync();

        Assert.Equal(RecordingState.Recording, viewModel.State);
        Assert.Equal("second.wav", audio.CurrentPath);

        releaseTranscription.SetResult();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(RecordingState.Recording, viewModel.State);
        Assert.Equal(0, viewModel.PendingTranscriptionCount);
        Assert.Contains("first.wav transcript", viewModel.RecentTranscripts[0]);
    }

    [Fact]
    public async Task RecordingFeedsAudioChunksToLivePreviewAndUpdatesPartialTranscript()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var preview = new FakeLiveTranscriptPreviewService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("final")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)123,
            preview);

        controller.StartRecording();
        audio.EmitAudioChunk([1, 2, 3, 4]);
        preview.EmitPartial("Testing one two.");

        Assert.True(preview.Started);
        Assert.Equal([1, 2, 3, 4], preview.Chunks.Single());
        Assert.Equal("Testing one two.", viewModel.PartialTranscript);

        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.True(preview.Stopped);
    }

    [Fact]
    public async Task LivePreviewFailureDoesNotBlockFinalTranscription()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var preview = new FakeLiveTranscriptPreviewService { ThrowOnStart = true };
        var paste = new FakeClipboardPasteService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("final transcript")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)123,
            preview);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(["final transcript "], paste.Texts);
        Assert.Equal(RecordingState.Idle, viewModel.State);
    }

    [Fact]
    public async Task SlowLivePreviewStopDoesNotDelayFinalTranscriptionQueue()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var previewCanStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preview = new FakeLiveTranscriptPreviewService { StopGate = previewCanStop.Task };
        var transcriptionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(audioPath =>
            {
                transcriptionStarted.SetResult();
                return Task.FromResult($"{audioPath} transcript");
            }),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)123,
            preview);

        controller.StartRecording();
        await controller.ToggleAsync();

        await transcriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(RecordingState.Idle, viewModel.State);

        previewCanStop.SetResult();
        await preview.StoppedTask.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.WaitForPendingTranscriptionsAsync();
        Assert.True(preview.Stopped);
    }

    [Fact]
    public async Task QueuedJobsPasteToTheirCapturedTargetWindowsInOrder()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav", "second.wav");
        var transcription = new FakeTranscriptionService(audioPath => Task.FromResult(audioPath));
        var paste = new FakeClipboardPasteService();
        var targets = new Queue<IntPtr>([(IntPtr)111, (IntPtr)222]);
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            transcription,
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => targets.Dequeue());

        controller.StartRecording();
        await controller.ToggleAsync();
        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal([(IntPtr)111, (IntPtr)222], paste.TargetWindows);
        Assert.Equal(["first.wav ", "second.wav "], paste.Texts);
    }

    [Fact]
    public async Task QueuedJobPastesThroughWindowDispatcher()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var transcription = new FakeTranscriptionService(_ => Task.FromResult("hello"));
        var paste = new FakeClipboardPasteService(() => window.IsInsideInvokeAsync);
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            transcription,
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.True(paste.WasPastedInsideWindowDispatcher);
    }

    [Fact]
    public async Task CompletedDictationReportsLatency()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var reporter = new FakeLatencyReporter();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            latencyReporter: reporter);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        var trace = Assert.Single(reporter.Traces);
        Assert.Equal(LatencyStatus.Completed, trace.Status);
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.RecordingStart));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.QueueStarted));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.WhisperFinished));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.PasteFinished));
    }

    [Fact]
    public async Task FailedDictationReportsLatency()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var reporter = new FakeLatencyReporter();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => throw new InvalidOperationException("boom")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            latencyReporter: reporter);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        var trace = Assert.Single(reporter.Traces);
        Assert.Equal(LatencyStatus.Failed, trace.Status);
        Assert.Equal("InvalidOperationException", trace.ErrorKind);
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.Failed));
    }

    private static SettingsStore CreateSettingsStore()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = modelPath
        });
        return store;
    }

    private sealed class FakeMiniRecorderWindow : IMiniRecorderWindow
    {
        public bool IsInsideInvokeAsync { get; private set; }

        public void ShowBottomCenter()
        {
        }

        public void Hide()
        {
        }

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public async Task InvokeAsync(Func<Task> action)
        {
            IsInsideInvokeAsync = true;
            try
            {
                await action();
            }
            finally
            {
                IsInsideInvokeAsync = false;
            }
        }
    }

    private sealed class FakeAudioCaptureService : IAudioCaptureService
    {
        private readonly Queue<string> _paths;

        public FakeAudioCaptureService(params string[] paths)
        {
            _paths = new Queue<string>(paths);
        }

        public event Action<double>? AudioLevelChanged;

        public event Action<byte[]>? AudioChunkAvailable;

        public string? CurrentPath { get; private set; }

        public string Start(string outputDirectory)
        {
            CurrentPath = _paths.Dequeue();
            AudioLevelChanged?.Invoke(0.5);
            return CurrentPath;
        }

        public void Stop()
        {
        }

        public void EmitAudioChunk(byte[] audioChunk)
        {
            AudioChunkAvailable?.Invoke(audioChunk);
        }
    }

    private sealed class FakeTranscriptionService : ITranscriptionService
    {
        private readonly Func<string, Task<string>> _transcribeAsync;

        public FakeTranscriptionService(Func<string, Task<string>> transcribeAsync)
        {
            _transcribeAsync = transcribeAsync;
        }

        public Task<string> TranscribeAsync(
            string whisperCliPath,
            string modelPath,
            string audioPath,
            string initialPrompt,
            int threads,
            CancellationToken cancellationToken)
        {
            return _transcribeAsync(audioPath);
        }
    }

    private sealed class FakeClipboardPasteService : IClipboardPasteService
    {
        private readonly Func<bool>? _isInsideWindowDispatcher;

        public FakeClipboardPasteService(Func<bool>? isInsideWindowDispatcher = null)
        {
            _isInsideWindowDispatcher = isInsideWindowDispatcher;
        }

        public List<string> Texts { get; } = [];

        public List<IntPtr> TargetWindows { get; } = [];

        public bool WasPastedInsideWindowDispatcher { get; private set; }

        public Task PasteAsync(
            string text,
            bool restoreClipboard,
            int restoreDelayMs,
            IntPtr targetWindow,
            CancellationToken cancellationToken)
        {
            WasPastedInsideWindowDispatcher = _isInsideWindowDispatcher?.Invoke() ?? false;
            Texts.Add(text);
            TargetWindows.Add(targetWindow);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLatencyReporter : ILatencyReporter
    {
        public List<LatencyTrace> Traces { get; } = [];

        public void Report(LatencyTrace trace)
        {
            Traces.Add(trace);
        }
    }

    private sealed class FakeLiveTranscriptPreviewService : ILiveTranscriptPreviewService
    {
        private Action<string>? _onPartialTranscript;
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public Task StoppedTask => _stopped.Task;

        public bool ThrowOnStart { get; init; }

        public Task? StopGate { get; init; }

        public List<byte[]> Chunks { get; } = [];

        public Task StartAsync(
            AppSettings settings,
            Action<string> onPartialTranscript,
            CancellationToken cancellationToken)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("preview failed");
            }

            Started = true;
            _onPartialTranscript = onPartialTranscript;
            return Task.CompletedTask;
        }

        public void AcceptAudioChunk(byte[] audioChunk)
        {
            Chunks.Add(audioChunk);
        }

        public async Task StopAsync()
        {
            if (StopGate is not null)
            {
                await StopGate;
            }

            Stopped = true;
            _stopped.TrySetResult();
        }

        public void EmitPartial(string text)
        {
            _onPartialTranscript?.Invoke(text);
        }
    }
}
