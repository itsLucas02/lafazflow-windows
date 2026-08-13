using System.Diagnostics;
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
        await preview.StartedTask.WaitAsync(TimeSpan.FromSeconds(1));
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
    public async Task RecordingStartupDoesNotWaitForLivePreviewStartup()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var previewCanStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preview = new FakeLiveTranscriptPreviewService { StartGate = previewCanStart.Task };
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

        var startTask = Task.Run(() => controller.StartRecording());
        await Task.Delay(100);

        Assert.True(startTask.IsCompleted);
        Assert.Equal(RecordingState.Recording, viewModel.State);
        Assert.Equal(1, window.ShowCount);
        Assert.Equal("first.wav", audio.CurrentPath);

        previewCanStart.SetResult();
        await preview.StartedTask.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();
    }

    [Fact]
    public async Task ToggleLogsHotkeyDiagnosticStateDecisions()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var diagnostics = new RecordingHotkeyDiagnostics();
        var releaseAudioStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        audio.StopGate = releaseAudioStop.Task;
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("final")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)123,
            hotkeyDiagnostics: diagnostics);

        await controller.ToggleAsync();
        var stopTask = controller.ToggleAsync();
        await audio.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.ToggleAsync();

        releaseAudioStop.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Contains(diagnostics.Events, entry => entry.Event == "toggle_start" && entry.State == "Idle");
        Assert.Contains(diagnostics.Events, entry => entry.Event == "toggle_stop" && entry.State == "Recording");
        Assert.Contains(diagnostics.Events, entry => entry.Event == "toggle_ignored" && entry.State == "Transcribing");
    }

    [Fact]
    public void StartRecordingValidationErrorLogsHotkeyDiagnostic()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var diagnostics = new RecordingHotkeyDiagnostics();
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = @"C:\missing\whisper-cli.exe",
            ModelPath = @"C:\missing\model.bin"
        });
        var controller = new RecorderController(
            viewModel,
            window,
            new FakeAudioCaptureService("first.wav"),
            new FakeTranscriptionService(_ => Task.FromResult("final")),
            new FakeClipboardPasteService(),
            store,
            new SoundCueService(),
            () => (IntPtr)123,
            hotkeyDiagnostics: diagnostics);

        controller.StartRecording();

        Assert.Contains(diagnostics.Events, entry =>
            entry.Event == "toggle_ignored"
            && entry.Accepted == "false"
            && entry.Reason == "validation_error");
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
    public async Task StopShowsProcessingCueBeforeAudioStopCompletes()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var releaseAudioStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new FakeAudioCaptureService("first.wav")
        {
            StopGate = releaseAudioStop.Task
        };
        var soundPlayer = new RecordingSoundCuePlayer();
        var soundCues = CreateSoundCueService(soundPlayer);
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            soundCues,
            () => (IntPtr)111);

        controller.StartRecording();
        var stopTask = controller.ToggleAsync();
        await audio.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var stateDuringStop = viewModel.State;
        var showProcessingDuringStop = viewModel.ShowProcessingIndicator;
        var playedPathsDuringStop = soundPlayer.PlayedPaths.ToArray();
        var stopCompletedDuringStop = audio.StopCompleted.Task.IsCompleted;

        releaseAudioStop.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(RecordingState.Transcribing, stateDuringStop);
        Assert.True(showProcessingDuringStop);
        Assert.EndsWith("recstop.wav", playedPathsDuringStop.Last());
        Assert.False(stopCompletedDuringStop);
    }

    [Fact]
    public async Task SoundCuesCanBeDisabledFromSettings()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var soundPlayer = new RecordingSoundCuePlayer();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(settings => settings with { EnableSoundCues = false }),
            CreateSoundCueService(soundPlayer),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Empty(soundPlayer.PlayedPaths);
    }

    [Fact]
    public async Task SoundCuesUseConfiguredVolume()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var soundPlayer = new RecordingSoundCuePlayer();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(settings => settings with { SoundCueVolume = 0.8 }),
            CreateSoundCueService(soundPlayer),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Collection(
            soundPlayer.Volumes,
            volume => Assert.Equal(0.8f, volume, precision: 6),
            volume => Assert.Equal(0.8f, volume, precision: 6),
            volume => Assert.Equal(1.0f, volume, precision: 6));
    }

    [Fact]
    public async Task CompletionCuePlaysOnlyAfterPasteSucceeds()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var soundPlayer = new RecordingSoundCuePlayer();
        var paste = new FakeClipboardPasteService(onPaste: () =>
        {
            Assert.DoesNotContain(soundPlayer.PlayedPaths, path => path.EndsWith("pastess.mp3"));
        });
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            paste,
            CreateSettingsStore(),
            CreateSoundCueService(soundPlayer),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.EndsWith("pastess.mp3", soundPlayer.PlayedPaths.Last());
    }

    [Fact]
    public async Task FailedDictationPlaysErrorCueWithoutCompletionCue()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var soundPlayer = new RecordingSoundCuePlayer();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => throw new InvalidOperationException("boom")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            CreateSoundCueService(soundPlayer),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Contains(soundPlayer.PlayedPaths, path => path.EndsWith("esc.wav"));
        Assert.DoesNotContain(soundPlayer.PlayedPaths, path => path.EndsWith("pastess.mp3"));
    }

    [Fact]
    public async Task SilentRecordingShowsMicrophoneErrorAndDoesNotPaste()
    {
        var silentAudioPath = WritePcm16Wav(Enumerable.Repeat((short)2, 16000));
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService(silentAudioPath);
        var soundPlayer = new RecordingSoundCuePlayer();
        var paste = new FakeClipboardPasteService();
        var transcriptionCalled = false;
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ =>
            {
                transcriptionCalled = true;
                return Task.FromResult("should not happen");
            }),
            paste,
            CreateSettingsStore(),
            CreateSoundCueService(soundPlayer),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.False(transcriptionCalled);
        Assert.Empty(paste.Texts);
        Assert.Equal(RecordingState.Error, viewModel.State);
        Assert.Contains("microphone", viewModel.StatusDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(soundPlayer.PlayedPaths, path => path.EndsWith("esc.wav"));
        Assert.DoesNotContain(soundPlayer.PlayedPaths, path => path.EndsWith("pastess.mp3"));
    }

    [Fact]
    public async Task NoSpeechErrorAutoDismissesAndHidesRecorderShell()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("   ")),
            paste,
            CreateSettingsStore(),
            CreateSoundCueService(new RecordingSoundCuePlayer()),
            () => (IntPtr)111,
            transientErrorDismissDelay: TimeSpan.FromMilliseconds(250));

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        await WaitUntilAsync(() => viewModel.State == RecordingState.Error);
        Assert.Equal("No speech", viewModel.StatusText);
        Assert.Empty(paste.Texts);

        await WaitUntilAsync(() => viewModel.State == RecordingState.Idle);

        Assert.Equal(RecordingState.Idle, viewModel.State);
        Assert.Equal("", viewModel.StatusText);
        Assert.Equal(1, window.HideCount);
    }

    [Fact]
    public async Task ToggleDuringStopHandoffDoesNotStartNewRecording()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var releaseAudioStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new FakeAudioCaptureService("first.wav", "second.wav")
        {
            StopGate = releaseAudioStop.Task
        };
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            CreateSoundCueService(new RecordingSoundCuePlayer()),
            () => (IntPtr)111);

        controller.StartRecording();
        var stopTask = controller.ToggleAsync();
        await audio.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondToggleTask = controller.ToggleAsync();
        await Task.Delay(100);
        var startCountDuringStop = audio.StartCount;
        var stateDuringStop = viewModel.State;

        releaseAudioStop.SetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(1));
        await secondToggleTask.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(1, startCountDuringStop);
        Assert.Equal(RecordingState.Transcribing, stateDuringStop);
    }

    [Fact]
    public async Task RecordingCanRestartAfterStoppedJobIsQueued()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav", "second.wav");
        var transcriptionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTranscription = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transcription = new FakeTranscriptionService(async audioPath =>
        {
            transcriptionStarted.TrySetResult();
            await releaseTranscription.Task;
            return $"{audioPath} transcript";
        });
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            transcription,
            new FakeClipboardPasteService(),
            CreateSettingsStore(),
            CreateSoundCueService(new RecordingSoundCuePlayer()),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await transcriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(RecordingState.Idle, viewModel.State);

        await controller.ToggleAsync();

        Assert.Equal(RecordingState.Recording, viewModel.State);
        Assert.Equal(2, audio.StartCount);
        Assert.Equal("second.wav", audio.CurrentPath);

        releaseTranscription.SetResult();
        await controller.WaitForPendingTranscriptionsAsync();
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

        await controller.ToggleAsync(Stopwatch.GetTimestamp());
        await controller.ToggleAsync(Stopwatch.GetTimestamp());
        await controller.WaitForPendingTranscriptionsAsync();

        var trace = Assert.Single(reporter.Traces);
        Assert.Equal(LatencyStatus.Completed, trace.Status);
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.HotkeyReceived));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.ToggleHandlingStarted));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.RecordingStart));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.RecorderShown));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.StopHotkeyReceived));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.StopRequested));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.QueueStarted));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.PreviewStartRequested));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.PreviewStarted));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.WhisperFinished));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.PasteFinished));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.UiHideStarted));
        Assert.True(trace.HasCheckpoint(LatencyCheckpoint.UiHidden));
    }

    [Fact]
    public async Task CompletedDictationHidesImmediatelyAfterPasteGestureBeforeClipboardRestore()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var pasteGestureSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRestore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paste = new FakeClipboardPasteService(
            onPaste: () => pasteGestureSent.SetResult(),
            restoreGate: releaseRestore.Task);
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("hello")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111);

        controller.StartRecording();
        await controller.ToggleAsync();
        await pasteGestureSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await Task.Delay(50);

        Assert.Equal(1, window.HideCount);
        Assert.False(paste.RestoreCompleted);

        releaseRestore.SetResult();
        await paste.RestoreCompletedTask.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.True(paste.RestoreCompleted);
    }

    [Fact]
    public async Task CompletedDictationUsesTargetTextContextForContinuationCasing()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("Hello, over.")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            targetTextContext: new FakeTargetTextContextService("Whatever,"));

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(["hello, over. "], paste.Texts);
        Assert.Equal("hello, over.", viewModel.RecentTranscripts[0]);
    }

    [Fact]
    public async Task CompletedDictationUsesCombinedCustomVocabularyPrompt()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var transcription = new FakeTranscriptionService(_ => Task.FromResult("hello"));
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            transcription,
            new FakeClipboardPasteService(),
            CreateSettingsStore(settings => settings with
            {
                CustomVocabularyTerms = "PDPA\r\nCare Visit\r\nalign"
            }),
            new SoundCueService(),
            () => (IntPtr)111,
            targetTextContext: new FakeTargetTextContextService(""));

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        var prompt = Assert.Single(transcription.InitialPrompts);
        Assert.Contains("Supabase", prompt);
        Assert.Contains("PDPA", prompt);
        Assert.Contains("Care Visit", prompt);
        Assert.Contains("align", prompt);
    }

    [Fact]
    public async Task CompletedDictationAppliesCustomCorrectionRules()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("Open superbiz.")),
            paste,
            CreateSettingsStore(settings => settings with
            {
                CustomCorrectionRules = "Supabase => Supabase database"
            }),
            new SoundCueService(),
            () => (IntPtr)111,
            targetTextContext: new FakeTargetTextContextService(""));

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(["Open Supabase database. "], paste.Texts);
        Assert.Equal("Open Supabase database.", viewModel.RecentTranscripts[0]);
    }

    [Fact]
    public async Task FinalDictationUsesTranscriptionEngineAndPastesExactlyOnce()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var engine = new FakeTranscriptionEngine((path, settings, id) =>
            Task.FromResult(new TranscriptionEngineResult(
                WhisperCliTranscriptionService.CleanTranscript("hello world"),
                true,
                null,
                null,
                null)));
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("unused")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            transcriptionEngine: engine);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        var request = Assert.Single(engine.Requests);
        Assert.Equal("first.wav", request.AudioPath);
        Assert.NotEqual(Guid.Empty, request.DictationId);
        Assert.Equal(["Hello world. "], paste.Texts);
    }

    [Fact]
    public async Task SuccessfulDictationRecordsPerformanceHealthSample()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        // Each recording is finalized and the audio file is deleted afterwards
        // unless diagnostics retention is enabled, so use one file per dictation.
        var firstAudioPath = Path.Combine(Path.GetTempPath(), $"lafazflow-health-{Guid.NewGuid():N}.wav");
        var secondAudioPath = Path.Combine(Path.GetTempPath(), $"lafazflow-health-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(firstAudioPath, BuildThreeSecondPcmWav());
        File.WriteAllBytes(secondAudioPath, BuildThreeSecondPcmWav());
        var audio = new FakeAudioCaptureService(firstAudioPath, secondAudioPath);
        var paste = new FakeClipboardPasteService();
        var monitor = new PerformanceHealthMonitor();
        var store = CreateSettingsStore();
        var engine = new FakeTranscriptionEngine((path, settings, id) =>
            Task.FromResult(new TranscriptionEngineResult(
                WhisperCliTranscriptionService.CleanTranscript("hello world"),
                true,
                null,
                null,
                InferenceMs: 120)));
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("unused")),
            paste,
            store,
            new SoundCueService(),
            () => (IntPtr)111,
            transcriptionEngine: engine,
            performanceHealthMonitor: monitor);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        // The first dictation is deliberately cold (excluded from the health window),
        // so run a second successful dictation to produce the first warm sample.
        await controller.ToggleAsync();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        var settings = store.Load();
        var fingerprint = EngineSettingsFingerprint.Compute(settings);
        var samples = monitor.RecentSamples(fingerprint);
        Assert.True(
            samples.Count == 1,
            $"engine requests={engine.Requests.Count}, transcripts={viewModel.RecentTranscripts.Count}, " +
            $"state={viewModel.State}, first={File.Exists(firstAudioPath)}, second={File.Exists(secondAudioPath)}");
        var sample = samples.Single();
        Assert.Equal(120, sample.InferenceMs);
        Assert.NotEqual(Guid.Empty, sample.DictationId);
        Assert.False(sample.IsCold);
    }

    private static byte[] BuildThreeSecondPcmWav()
    {
        var sampleCount = 16000 * 3;
        var dataSize = sampleCount * 2;
        var bytes = new byte[44 + dataSize];
        Buffer.BlockCopy("RIFF"u8.ToArray(), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(36 + dataSize), 0, bytes, 4, 4);
        Buffer.BlockCopy("WAVE"u8.ToArray(), 0, bytes, 8, 4);
        Buffer.BlockCopy("fmt "u8.ToArray(), 0, bytes, 12, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(16), 0, bytes, 16, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, bytes, 20, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, bytes, 22, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(16000), 0, bytes, 24, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(32000), 0, bytes, 28, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((short)2), 0, bytes, 32, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, bytes, 34, 2);
        Buffer.BlockCopy("data"u8.ToArray(), 0, bytes, 36, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(dataSize), 0, bytes, 40, 4);
        var tone = new byte[2];
        for (var i = 0; i < sampleCount; i++)
        {
            // A gentle 440 Hz tone keeps the fixture audible (non-silent) without clipping.
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000.0) * short.MaxValue * 0.35);
            BitConverter.GetBytes(sample).CopyTo(tone, 0);
            Buffer.BlockCopy(tone, 0, bytes, 44 + (i * 2), 2);
        }

        return bytes;
    }

    [Fact]
    public async Task EmptyEngineResultDoesNotPaste()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var engine = new FakeTranscriptionEngine((path, settings, id) =>
            Task.FromResult(new TranscriptionEngineResult("", true, null, null, null)));
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("unused")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            transcriptionEngine: engine);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Empty(paste.Texts);
        Assert.Equal(RecordingState.Error, viewModel.State);
        Assert.Contains("No speech", viewModel.StatusDetail);
    }

    [Fact]
    public async Task FailedEngineResultDoesNotPaste()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var engine = new FakeTranscriptionEngine((path, settings, id) =>
            Task.FromResult(new TranscriptionEngineResult("", false, "worker_unavailable", null, null)));
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("unused")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            transcriptionEngine: engine);

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Empty(paste.Texts);
        Assert.Equal(RecordingState.Error, viewModel.State);
        Assert.Contains("worker_unavailable", viewModel.StatusDetail);
    }

    [Fact]
    public async Task CompletedDictationSkipsCustomCorrectionRulesWhenCorrectionsAreDisabled()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("Open superbiz.")),
            paste,
            CreateSettingsStore(settings => settings with
            {
                EnableVocabularyCorrections = false,
                CustomCorrectionRules = "superbiz => Supabase"
            }),
            new SoundCueService(),
            () => (IntPtr)111,
            targetTextContext: new FakeTargetTextContextService(""));

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(["Open superbiz. "], paste.Texts);
    }

    [Fact]
    public async Task CompletedDictationUsesPostProcessingPipelineBeforePaste()
    {
        var viewModel = new MiniRecorderViewModel();
        var window = new FakeMiniRecorderWindow();
        var audio = new FakeAudioCaptureService("first.wav");
        var paste = new FakeClipboardPasteService();
        var controller = new RecorderController(
            viewModel,
            window,
            audio,
            new FakeTranscriptionService(_ => Task.FromResult("Um, open superbiz.")),
            paste,
            CreateSettingsStore(),
            new SoundCueService(),
            () => (IntPtr)111,
            targetTextContext: new FakeTargetTextContextService(""));

        controller.StartRecording();
        await controller.ToggleAsync();
        await controller.WaitForPendingTranscriptionsAsync();

        Assert.Equal(["Open Supabase. "], paste.Texts);
        Assert.Equal("Open Supabase.", viewModel.RecentTranscripts[0]);
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

    private static SettingsStore CreateSettingsStore(Func<AppSettings, AppSettings>? configure = null)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var settings = AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = modelPath
        };
        store.Save(configure?.Invoke(settings) ?? settings);
        return store;
    }

    private static SoundCueService CreateSoundCueService(RecordingSoundCuePlayer player)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "recstart.wav"), [1]);
        File.WriteAllBytes(Path.Combine(root, "recstop.wav"), [1]);
        File.WriteAllBytes(Path.Combine(root, "pastess.mp3"), [1]);
        File.WriteAllBytes(Path.Combine(root, "esc.wav"), [1]);
        return new SoundCueService(root, player);
    }

    private static string WritePcm16Wav(IEnumerable<short> samples)
    {
        var sampleArray = samples.ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var dataLength = sampleArray.Length * sizeof(short);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(16000 * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataLength);

        foreach (var sample in sampleArray)
        {
            writer.Write(sample);
        }

        return path;
    }

    private sealed class FakeMiniRecorderWindow : IMiniRecorderWindow
    {
        public bool IsInsideInvokeAsync { get; private set; }

        public int HideCount { get; private set; }

        public int ShowCount { get; private set; }

        public void ShowBottomCenter()
        {
            ShowCount++;
        }

        public void Hide()
        {
            HideCount++;
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

        public int StartCount { get; private set; }

        public Task? StopGate { get; set; }

        public TaskCompletionSource StopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource StopCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Start(string outputDirectory)
        {
            StartCount++;
            CurrentPath = _paths.Dequeue();
            AudioLevelChanged?.Invoke(0.5);
            return CurrentPath;
        }

        public async Task<AudioCaptureFinalization> StopAsync()
        {
            StopStarted.TrySetResult();
            if (StopGate is not null)
            {
                await StopGate;
            }

            StopCompleted.TrySetResult();
            return new AudioCaptureFinalization(
                CurrentPath ?? "",
                SampleCount: 1600,
                ByteCount: 3200,
                DurationMilliseconds: 100,
                AudioCaptureFinalizeState.Finalized,
                ErrorKind: "");
        }

        public void EmitAudioChunk(byte[] audioChunk)
        {
            AudioChunkAvailable?.Invoke(audioChunk);
        }
    }

    private sealed class FakeTranscriptionEngine : ITranscriptionEngine
    {
        private readonly Func<string, AppSettings, Guid, Task<TranscriptionEngineResult>> _transcribe;

        public FakeTranscriptionEngine(
            Func<string, AppSettings, Guid, Task<TranscriptionEngineResult>> transcribe)
        {
            _transcribe = transcribe;
        }

        public List<(string AudioPath, AppSettings Settings, Guid DictationId)> Requests { get; } = [];

        public Task<TranscriptionEngineResult> TranscribeAsync(
            string audioPath,
            AppSettings settings,
            Guid dictationId,
            CancellationToken cancellationToken)
        {
            Requests.Add((audioPath, settings, dictationId));
            return _transcribe(audioPath, settings, dictationId);
        }
    }

    private sealed class FakeTranscriptionService : ITranscriptionService
    {
        private readonly Func<string, Task<string>> _transcribeAsync;

        public FakeTranscriptionService(Func<string, Task<string>> transcribeAsync)
        {
            _transcribeAsync = transcribeAsync;
        }

        public List<string> InitialPrompts { get; } = [];

        public Task<string> TranscribeAsync(
            string whisperCliPath,
            string modelPath,
            string audioPath,
            string initialPrompt,
            int threads,
            WhisperDecodeOptions decodeOptions,
            CancellationToken cancellationToken)
        {
            InitialPrompts.Add(initialPrompt);
            return _transcribeAsync(audioPath);
        }
    }

    private sealed class FakeClipboardPasteService : IClipboardPasteService
    {
        private readonly Func<bool>? _isInsideWindowDispatcher;
        private readonly Action? _onPaste;
        private readonly Task? _restoreGate;

        public FakeClipboardPasteService(
            Func<bool>? isInsideWindowDispatcher = null,
            Action? onPaste = null,
            Task? restoreGate = null)
        {
            _isInsideWindowDispatcher = isInsideWindowDispatcher;
            _onPaste = onPaste;
            _restoreGate = restoreGate;
        }

        public List<string> Texts { get; } = [];

        public List<IntPtr> TargetWindows { get; } = [];

        public bool WasPastedInsideWindowDispatcher { get; private set; }

        public bool RestoreCompleted { get; private set; }

        public TaskCompletionSource RestoreCompletedTask { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ClipboardPasteResult> PasteAsync(
            string text,
            bool restoreClipboard,
            int restoreDelayMs,
            IntPtr targetWindow,
            CancellationToken cancellationToken)
        {
            _onPaste?.Invoke();
            WasPastedInsideWindowDispatcher = _isInsideWindowDispatcher?.Invoke() ?? false;
            Texts.Add(text);
            TargetWindows.Add(targetWindow);
            if (_restoreGate is not null)
            {
                _ = Task.Run(async () =>
                {
                    await _restoreGate;
                    RestoreCompleted = true;
                    RestoreCompletedTask.SetResult();
                }, CancellationToken.None);
            }

            return Task.FromResult(new ClipboardPasteResult(
                restoreClipboard,
                restoreDelayMs,
                PasteKeyGesture.ControlV,
                "test"));
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

    private sealed class RecordingHotkeyDiagnostics : IHotkeyDiagnostics
    {
        public List<HotkeyDiagnosticWrite> Events { get; } = [];

        public void Log(HotkeyDiagnosticWrite entry)
        {
            Events.Add(entry);
        }
    }

    private sealed class FakeTargetTextContextService(string textBeforeCaret) : ITargetTextContextService
    {
        public string GetTextBeforeCaret(IntPtr targetWindow)
        {
            return textBeforeCaret;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private sealed class RecordingSoundCuePlayer : ISoundCuePlayer
    {
        public List<string> PlayedPaths { get; } = [];

        public List<float> Volumes { get; } = [];

        public void Play(string path, float volume)
        {
            PlayedPaths.Add(path);
            Volumes.Add(volume);
        }
    }

    private sealed class FakeLiveTranscriptPreviewService : ILiveTranscriptPreviewService
    {
        private Action<string>? _onPartialTranscript;
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public Task StartedTask => _started.Task;

        public Task StoppedTask => _stopped.Task;

        public bool ThrowOnStart { get; init; }

        public Task? StartGate { get; init; }

        public Task? StopGate { get; init; }

        public List<byte[]> Chunks { get; } = [];

        public async Task StartAsync(
            AppSettings settings,
            Action<string> onPartialTranscript,
            CancellationToken cancellationToken)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("preview failed");
            }

            if (StartGate is not null)
            {
                await StartGate;
            }

            Started = true;
            _onPartialTranscript = onPartialTranscript;
            _started.TrySetResult();
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
