using System.IO;

namespace LafazFlow.Windows.Services;

public sealed class FileLatencyReporter : ILatencyReporter
{
    public void Report(LatencyTrace trace)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LafazFlow",
                "Logs");
            BoundedLogFileWriter.AppendLine(
                Path.Combine(logRoot, "lafazflow.log"),
                $"[{DateTimeOffset.Now:O}] {LatencyLogFormatter.Format(trace)}");
        }
        catch
        {
        }
    }
}
