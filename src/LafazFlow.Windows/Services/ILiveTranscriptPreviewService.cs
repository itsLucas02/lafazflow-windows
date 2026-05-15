using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public interface ILiveTranscriptPreviewService
{
    Task StartAsync(
        AppSettings settings,
        Action<string> onPartialTranscript,
        CancellationToken cancellationToken);

    void AcceptAudioChunk(byte[] audioChunk);

    Task StopAsync();
}
