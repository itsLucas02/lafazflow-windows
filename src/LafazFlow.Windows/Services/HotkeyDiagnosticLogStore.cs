using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public sealed partial class HotkeyDiagnosticLogStore
{
    public const int DefaultLimit = 20;

    private readonly string _logPath;

    public HotkeyDiagnosticLogStore(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Logs",
            "lafazflow.log");
    }

    public IReadOnlyList<HotkeyDiagnosticEvent> LoadRecent(int limit = DefaultLimit)
    {
        if (!File.Exists(_logPath))
        {
            return [];
        }

        return ReadAllLinesShared(_logPath)
            .Select(ParseLine)
            .Where(row => row is not null)
            .Cast<HotkeyDiagnosticEvent>()
            .OrderByDescending(row => row.Timestamp)
            .Take(Math.Max(0, limit))
            .ToArray();
    }

    public int ClearHotkeyLines()
    {
        if (!File.Exists(_logPath))
        {
            return 0;
        }

        var lines = ReadAllLinesShared(_logPath);
        var retained = lines
            .Where(line => !IsHotkeyLine(line))
            .ToArray();
        File.WriteAllLines(_logPath, retained);

        return lines.Length - retained.Length;
    }

    public static HotkeyDiagnosticEvent? ParseLine(string line)
    {
        if (!IsHotkeyLine(line))
        {
            return null;
        }

        var timestamp = ParseTimestamp(line);
        var hotkeyStart = line.IndexOf("HOTKEY ", StringComparison.Ordinal);
        var fields = KeyValueRegex()
            .Matches(line[(hotkeyStart + "HOTKEY ".Length)..])
            .ToDictionary(
                match => match.Groups["key"].Value,
                match => match.Groups["value"].Value,
                StringComparer.OrdinalIgnoreCase);

        return new HotkeyDiagnosticEvent(
            timestamp,
            Value(fields, "event"),
            Value(fields, "gesture"),
            Value(fields, "accepted"),
            Value(fields, "state"),
            Value(fields, "dispatch_ms"),
            Value(fields, "reason"),
            Value(fields, "target"));
    }

    private static string[] ReadAllLinesShared(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines.ToArray();
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static bool IsHotkeyLine(string line)
    {
        return line.Contains("HOTKEY ", StringComparison.Ordinal);
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
