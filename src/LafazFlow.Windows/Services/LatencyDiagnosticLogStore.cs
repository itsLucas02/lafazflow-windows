using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public sealed partial class LatencyDiagnosticLogStore
{
    public const int DefaultLimit = 20;

    private readonly string _logPath;

    public LatencyDiagnosticLogStore(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Logs",
            "lafazflow.log");
    }

    public IReadOnlyList<LatencyDiagnosticRow> LoadRecent(int limit = DefaultLimit)
    {
        if (!File.Exists(_logPath))
        {
            return [];
        }

        return File.ReadLines(_logPath)
            .Select(ParseLine)
            .Where(row => row is not null)
            .Cast<LatencyDiagnosticRow>()
            .OrderByDescending(row => row.Timestamp)
            .Take(Math.Max(0, limit))
            .ToArray();
    }

    public int ClearLatencyLines()
    {
        if (!File.Exists(_logPath))
        {
            return 0;
        }

        var lines = File.ReadAllLines(_logPath);
        var retained = lines
            .Where(line => !IsLatencyLine(line))
            .ToArray();
        File.WriteAllLines(_logPath, retained);

        return lines.Length - retained.Length;
    }

    public static LatencyDiagnosticRow? ParseLine(string line)
    {
        if (!IsLatencyLine(line))
        {
            return null;
        }

        var timestamp = ParseTimestamp(line);
        var latencyStart = line.IndexOf("LATENCY ", StringComparison.Ordinal);
        var fields = KeyValueRegex()
            .Matches(line[(latencyStart + "LATENCY ".Length)..])
            .ToDictionary(
                match => match.Groups["key"].Value,
                match => match.Groups["value"].Value,
                StringComparer.OrdinalIgnoreCase);

        return new LatencyDiagnosticRow(
            timestamp,
            Value(fields, "id"),
            Value(fields, "status"),
            Value(fields, "model"),
            Value(fields, "threads"),
            Value(fields, "target"),
            Value(fields, "recording_ms"),
            Value(fields, "queue_wait_ms"),
            Value(fields, "whisper_ms"),
            Value(fields, "paste_ms"),
            Value(fields, "total_stop_to_done_ms"),
            Value(fields, "total_record_to_done_ms"),
            Value(fields, "error"));
    }

    private static bool IsLatencyLine(string line)
    {
        return line.Contains("LATENCY ", StringComparison.Ordinal);
    }

    private static DateTimeOffset ParseTimestamp(string line)
    {
        var match = TimestampRegex().Match(line);
        return match.Success
            && DateTimeOffset.TryParse(
                match.Groups["timestamp"].Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp)
            ? timestamp
            : DateTimeOffset.MinValue;
    }

    private static string Value(IReadOnlyDictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "na";
    }

    [GeneratedRegex(@"^\[(?<timestamp>[^\]]+)\]")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"(?<key>[A-Za-z0-9_]+)=(?<value>\S+)")]
    private static partial Regex KeyValueRegex();
}
