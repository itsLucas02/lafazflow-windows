using System.Globalization;
using System.IO;

namespace LafazFlow.Windows.Services;

public static class BoundedLogFileWriter
{
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private const long FallbackTailBytes = 1024 * 1024;
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(7);

    public static void AppendLine(string logPath, string line)
    {
        try
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            TrimIfNeeded(logPath, DateTimeOffset.Now);

            using var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(line);
        }
        catch
        {
        }
    }

    internal static void TrimIfNeeded(string logPath, DateTimeOffset now)
    {
        try
        {
            if (!File.Exists(logPath) || new FileInfo(logPath).Length <= MaxLogBytes)
            {
                return;
            }

            var cutoff = now - RetentionWindow;
            var retainedLines = File.ReadLines(logPath)
                .Where(line => ShouldRetainLine(line, cutoff))
                .ToList();

            retainedLines.Insert(0, $"[{now:O}] LOG_RETENTION trimmed=true retention_days=7 max_mb=2");
            File.WriteAllLines(logPath, retainedLines);

            if (new FileInfo(logPath).Length > MaxLogBytes)
            {
                TrimToTail(logPath, now);
            }
        }
        catch
        {
        }
    }

    private static bool ShouldRetainLine(string line, DateTimeOffset cutoff)
    {
        if (!TryReadTimestamp(line, out var timestamp))
        {
            return false;
        }

        return timestamp >= cutoff;
    }

    private static bool TryReadTimestamp(string line, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (!line.StartsWith('['))
        {
            return false;
        }

        var closingBracket = line.IndexOf(']');
        if (closingBracket <= 1)
        {
            return false;
        }

        var timestampText = line[1..closingBracket];
        return DateTimeOffset.TryParse(
            timestampText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
    }

    private static void TrimToTail(string logPath, DateTimeOffset now)
    {
        var content = File.ReadAllText(logPath);
        var startIndex = Math.Max(0, content.Length - (int)Math.Min(FallbackTailBytes, content.Length));
        var tail = content[startIndex..];
        var firstNewLine = tail.IndexOf('\n');
        if (firstNewLine >= 0 && firstNewLine + 1 < tail.Length)
        {
            tail = tail[(firstNewLine + 1)..];
        }

        File.WriteAllText(
            logPath,
            $"[{now:O}] LOG_RETENTION trimmed=true retention_days=7 max_mb=2 fallback=tail{Environment.NewLine}{tail}");
    }
}
