namespace LafazFlow.Windows.Services;

public sealed class RollingWhisperLiveTranscriptPreviewOptions
{
    public int PreviewIntervalMilliseconds { get; init; } = 2200;

    public int MinimumAudioMilliseconds { get; init; } = 1800;

    public int RollingWindowMilliseconds { get; init; } = 6000;

    public int MinimumNewAudioMilliseconds { get; init; } = 1000;
}
