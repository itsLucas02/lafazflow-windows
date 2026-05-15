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
}
