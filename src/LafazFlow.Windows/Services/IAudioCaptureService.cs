namespace LafazFlow.Windows.Services;

public interface IAudioCaptureService
{
    event Action<double>? AudioLevelChanged;

    event Action<byte[]>? AudioChunkAvailable;

    string Start(string outputDirectory);

    void Stop();
}
