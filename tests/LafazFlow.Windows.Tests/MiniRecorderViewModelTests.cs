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

        Assert.Equal(0.78, viewModel.AudioLevel, precision: 6);
    }

    [Fact]
    public void AudioLevelUsesFastAttackAndSlowRelease()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.AudioLevel = 0.2;
        viewModel.AudioLevel = 1;
        var attackLevel = viewModel.AudioLevel;
        viewModel.AudioLevel = 0;

        Assert.True(attackLevel > 0.65);
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
