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
    public void TranscribingStateReportsProcessingStatus()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.State = RecordingState.Transcribing;

        Assert.False(viewModel.IsRecording);
        Assert.True(viewModel.IsProcessing);
        Assert.True(viewModel.HasStatusText);
        Assert.Equal("Transcribing", viewModel.StatusText);
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
    public void ProcessingPulseCyclesStatusText()
    {
        var viewModel = new MiniRecorderViewModel
        {
            State = RecordingState.Transcribing
        };

        viewModel.AdvanceProcessingPulse();

        Assert.Equal("Transcribing.", viewModel.StatusText);

        viewModel.AdvanceProcessingPulse();

        Assert.Equal("Transcribing..", viewModel.StatusText);
    }

    [Fact]
    public void SetErrorReportsErrorDetail()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.SetError("Whisper model was not found.");

        Assert.Equal(RecordingState.Error, viewModel.State);
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
}
