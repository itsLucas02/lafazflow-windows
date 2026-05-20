using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class TrayStatusTextTests
{
    [Fact]
    public void FromViewModelReportsIdle()
    {
        var viewModel = new MiniRecorderViewModel();

        Assert.Equal($"LafazFlow {viewModel.AppVersion} - Idle", TrayStatusText.FromViewModel(viewModel));
    }

    [Fact]
    public void FromViewModelReportsRecording()
    {
        var viewModel = new MiniRecorderViewModel
        {
            State = RecordingState.Recording
        };

        Assert.Equal($"LafazFlow {viewModel.AppVersion} - Recording", TrayStatusText.FromViewModel(viewModel));
    }

    [Theory]
    [InlineData(RecordingState.Transcribing)]
    [InlineData(RecordingState.Enhancing)]
    public void FromViewModelReportsTranscribingForProcessingStates(RecordingState state)
    {
        var viewModel = new MiniRecorderViewModel
        {
            State = state
        };

        Assert.Equal($"LafazFlow {viewModel.AppVersion} - Transcribing", TrayStatusText.FromViewModel(viewModel));
    }

    [Fact]
    public void FromViewModelReportsTranscribingForPendingJobs()
    {
        var viewModel = new MiniRecorderViewModel
        {
            PendingTranscriptionCount = 1
        };

        Assert.Equal($"LafazFlow {viewModel.AppVersion} - Transcribing", TrayStatusText.FromViewModel(viewModel));
    }

    [Fact]
    public void FromViewModelReportsError()
    {
        var viewModel = new MiniRecorderViewModel();

        viewModel.SetError("Whisper model was not found.");

        Assert.Equal($"LafazFlow {viewModel.AppVersion} - Error", TrayStatusText.FromViewModel(viewModel));
    }
}
