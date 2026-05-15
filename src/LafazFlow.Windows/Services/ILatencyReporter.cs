namespace LafazFlow.Windows.Services;

public interface ILatencyReporter
{
    void Report(LatencyTrace trace);
}
