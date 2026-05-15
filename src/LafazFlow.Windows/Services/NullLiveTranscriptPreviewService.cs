using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class NullLiveTranscriptPreviewService : ILiveTranscriptPreviewService
{
    public Task StartAsync(
        AppSettings settings,
        Action<string> onPartialTranscript,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void AcceptAudioChunk(byte[] audioChunk)
    {
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
