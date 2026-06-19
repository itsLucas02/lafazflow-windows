using System.IO;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public sealed partial class FileHotkeyDiagnostics : IHotkeyDiagnostics
{
    private readonly string _logPath;

    public FileHotkeyDiagnostics(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Logs",
            "lafazflow.log");
    }

    public void Log(HotkeyDiagnosticWrite entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(
                $"[{DateTimeOffset.Now:O}] HOTKEY event={Safe(entry.Event)} gesture={Safe(entry.Gesture)} accepted={Safe(entry.Accepted)} state={Safe(entry.State)} dispatch_ms={Safe(entry.DispatchMs)} reason={Safe(entry.Reason)} target={Safe(entry.Target)}");
        }
        catch
        {
        }
    }

    private static string Safe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "na";
        }

        var trimmed = value.Trim();
        if (trimmed.Contains('\\')
            || trimmed.Contains('/')
            || trimmed.Contains(':'))
        {
            return "path_redacted";
        }

        return UnsafeTokenRegex().Replace(trimmed, "_");
    }

    [GeneratedRegex(@"[^A-Za-z0-9_.-]+")]
    private static partial Regex UnsafeTokenRegex();
}
