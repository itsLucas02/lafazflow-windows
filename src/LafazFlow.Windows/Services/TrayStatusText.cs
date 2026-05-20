using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Services;

public static class TrayStatusText
{
    public static string FromViewModel(MiniRecorderViewModel viewModel)
    {
        var status = viewModel.State switch
        {
            RecordingState.Recording => "Recording",
            RecordingState.Transcribing or RecordingState.Enhancing => "Transcribing",
            RecordingState.Error => "Error",
            _ when viewModel.HasPendingTranscriptions => "Transcribing",
            _ => "Idle"
        };
        return $"{AppVersionText.TrayHeader} - {status}";
    }
}
