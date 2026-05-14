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
        Assert.Equal("", viewModel.StatusText);
    }

    [Fact]
    public void TranscribingStateReportsProcessingStatus()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.State = RecordingState.Transcribing;

        Assert.False(viewModel.IsRecording);
        Assert.True(viewModel.IsProcessing);
        Assert.Equal("Transcribing", viewModel.StatusText);
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
