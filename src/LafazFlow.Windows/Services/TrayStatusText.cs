using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Services;

public static class TrayStatusText
{
    public static string FromViewModel(MiniRecorderViewModel viewModel)
    {
        return viewModel.State switch
        {
            RecordingState.Recording => "LafazFlow - Recording",
            RecordingState.Transcribing or RecordingState.Enhancing => "LafazFlow - Transcribing",
            RecordingState.Error => "LafazFlow - Error",
            _ when viewModel.HasPendingTranscriptions => "LafazFlow - Transcribing",
            _ => "LafazFlow - Idle"
        };
    }
}
