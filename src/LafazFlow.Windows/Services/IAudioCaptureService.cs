namespace LafazFlow.Windows.Services;

public interface IAudioCaptureService
{
    event Action<double>? AudioLevelChanged;

    string Start(string outputDirectory);

    void Stop();
}
