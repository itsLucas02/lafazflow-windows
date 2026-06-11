using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class MiniRecorderViewModelTests
{
    [Fact]
    public void RecordingStateReportsRecordingAndNotProcessing()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.State = RecordingState.Recording;

        Assert.True(viewModel.IsRecording);
        Assert.False(viewModel.IsProcessing);
        Assert.False(viewModel.HasStatusText);
        Assert.Equal("", viewModel.StatusText);
    }

    [Fact]
    public void TranscribingStateReportsProcessingIndicator()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.State = RecordingState.Transcribing;

        Assert.False(viewModel.IsRecording);
        Assert.True(viewModel.IsProcessing);
        Assert.True(viewModel.ShowProcessingIndicator);
        Assert.False(viewModel.HasStatusText);
        Assert.Equal("", viewModel.StatusText);
    }

    [Fact]
    public void PendingTranscriptionsShowProcessingIndicator()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.PendingTranscriptionCount = 2;

        Assert.Equal(2, viewModel.PendingTranscriptionCount);
        Assert.True(viewModel.HasPendingTranscriptions);
        Assert.True(viewModel.ShowProcessingIndicator);
    }

    [Fact]
    public void RecordingHidesPendingProcessingIndicator()
    {
        var viewModel = new MiniRecorderViewModel
        {
            PendingTranscriptionCount = 1,
            State = RecordingState.Recording
        };

        Assert.True(viewModel.HasPendingTranscriptions);
        Assert.False(viewModel.ShowProcessingIndicator);
    }

    [Fact]
    public void CompletedTranscriptsAreQueuedNewestFirst()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.AddCompletedTranscript("First transcript.");
        viewModel.AddCompletedTranscript("Second transcript.");

        Assert.Equal("Second transcript.", viewModel.LastTranscriptPreview);
        Assert.Equal(["Second transcript.", "First transcript."], viewModel.RecentTranscripts);
        Assert.True(viewModel.HasRecentTranscripts);
    }

    [Fact]
    public void CompletedTranscriptQueueKeepsFiveItems()
    {
        var viewModel = new MiniRecorderViewModel();

        for (var index = 1; index <= 7; index++)
        {
            viewModel.AddCompletedTranscript($"Transcript {index}.");
        }

        Assert.Equal(5, viewModel.RecentTranscripts.Count);
        Assert.Equal("Transcript 7.", viewModel.RecentTranscripts[0]);
        Assert.Equal("Transcript 3.", viewModel.RecentTranscripts[^1]);
    }

    [Fact]
    public void ProcessingPulseCyclesIndicatorStep()
    {
        var viewModel = new MiniRecorderViewModel
        {
            State = RecordingState.Transcribing
        };

        for (var index = 0; index < 7; index++)
        {
            viewModel.AdvanceProcessingPulse();
        }

        Assert.Equal(0, viewModel.ProcessingPulseStep);
        Assert.Equal("", viewModel.StatusText);
    }

    [Fact]
    public void ProcessingPulseCyclesForPendingTranscriptions()
    {
        var viewModel = new MiniRecorderViewModel
        {
            PendingTranscriptionCount = 1
        };

        viewModel.AdvanceProcessingPulse();

        Assert.Equal(1, viewModel.ProcessingPulseStep);
    }

    [Fact]
    public void SetErrorReportsErrorDetail()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.SetError("Whisper model was not found.");

        Assert.Equal(RecordingState.Error, viewModel.State);
        Assert.False(viewModel.ShowProcessingIndicator);
        Assert.True(viewModel.HasStatusText);
        Assert.Equal("Whisper model was not found.", viewModel.StatusText);
        Assert.Equal("Whisper model was not found.", viewModel.StatusDetail);
    }

    [Fact]
    public void SetErrorSummarizesClipboardErrorsForCompactBoard()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.SetError("Clipboard data could not be read.");

        Assert.Equal("Clipboard error", viewModel.StatusText);
        Assert.Equal("Clipboard data could not be read.", viewModel.StatusDetail);
    }

    [Fact]
    public void SetErrorSummarizesNoSpeechForCompactBoard()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.SetError("No speech was transcribed. Check the microphone input and try again.");

        Assert.Equal("No speech", viewModel.StatusText);
        Assert.Equal("No speech was transcribed. Check the microphone input and try again.", viewModel.StatusDetail);
    }

    [Fact]
    public void SetErrorSummarizesSilentMicrophoneForCompactBoard()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.SetError("Microphone input was silent. Check the Windows input device, mic mute, and input volume.");

        Assert.Equal("Mic silent", viewModel.StatusText);
        Assert.Equal(
            "Microphone input was silent. Check the Windows input device, mic mute, and input volume.",
            viewModel.StatusDetail);
    }

    [Fact]
    public void AppVersionUsesCompactMajorMinorPatchFormat()
    {
        var viewModel = new MiniRecorderViewModel();
        var assemblyVersion = typeof(MiniRecorderViewModel).Assembly.GetName().Version;

        Assert.Matches(@"^v\d+\.\d+\.\d+$", viewModel.AppVersion);
        Assert.NotNull(assemblyVersion);
        Assert.Equal($"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}", viewModel.AppVersion);
    }

    [Theory]
    [InlineData(-0.4, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.8, 1)]
    public void AudioLevelIsClamped(double input, double expected)
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.AudioLevel = input;

        Assert.Equal(expected, viewModel.AudioLevel);
    }

    [Fact]
    public void AudioLevelUsesSlowReleaseSmoothing()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.AudioLevel = 1;
        viewModel.AudioLevel = 0;

        Assert.Equal(0.82, viewModel.AudioLevel, precision: 6);
    }

    [Fact]
    public void AudioLevelUsesFastAttackAndSlowRelease()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.AudioLevel = 0.2;
        viewModel.AudioLevel = 1;
        var attackLevel = viewModel.AudioLevel;
        viewModel.AudioLevel = 0;

        Assert.True(attackLevel > 0.75);
        Assert.True(viewModel.AudioLevel > 0.35);
    }

    [Fact]
    public void AudioLevelNoiseGateSettlesQuietInputToSilence()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.AudioLevel = 0.02;

        Assert.Equal(0, viewModel.AudioLevel);
    }

    [Fact]
    public void PartialTranscriptOnlyShowsLiveTextWhileRecording()
    {
        var viewModel = new MiniRecorderViewModel
        {
            PartialTranscript = "Testing one two.",
            State = RecordingState.Idle
        };

        Assert.False(viewModel.HasLiveTranscript);

        viewModel.State = RecordingState.Recording;

        Assert.True(viewModel.HasLiveTranscript);
    }

    [Fact]
    public void PartialTranscriptClearsWhenReturningToIdle()
    {
        var viewModel = new MiniRecorderViewModel
        {
            State = RecordingState.Recording,
            PartialTranscript = "Live words"
        };

        viewModel.State = RecordingState.Idle;

        Assert.Equal("", viewModel.PartialTranscript);
        Assert.False(viewModel.HasLiveTranscript);
    }

    [Fact]
    public void AudioLevelResetsWhenRecordingStops()
    {
        var viewModel = new MiniRecorderViewModel
        {
            State = RecordingState.Recording,
            AudioLevel = 1
        };

        viewModel.State = RecordingState.Idle;

        Assert.Equal(0, viewModel.AudioLevel);
    }

    [Fact]
    public void SettingsRequestCanBeRaisedFromRecorder()
    {
        var viewModel = new MiniRecorderViewModel();
        var raised = false;
        viewModel.SettingsRequested += () => raised = true;

        viewModel.RequestSettings();

        Assert.True(raised);
    }
}
