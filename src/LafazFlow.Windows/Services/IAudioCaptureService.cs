namespace LafazFlow.Windows.Services;

public enum AudioCaptureState
{
    Idle,
    Recording,
    Stopping,
    Finalized,
    Failed
}

public enum AudioCaptureFinalizeState
{
    Finalized,
    Failed
}

public sealed record AudioCaptureFinalization(
    string OutputPath,
    long SampleCount,
    long ByteCount,
    long DurationMilliseconds,
    AudioCaptureFinalizeState State,
    string ErrorKind);

public interface IAudioCaptureService
{
    event Action<double>? AudioLevelChanged;

    event Action<byte[]>? AudioChunkAvailable;

    string Start(string outputDirectory);

    Task<AudioCaptureFinalization> StopAsync();
}
