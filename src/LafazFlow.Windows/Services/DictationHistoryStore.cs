using System.IO;
using System.Text.Json;

namespace LafazFlow.Windows.Services;

public sealed record DictationHistoryEntry(DateTimeOffset Timestamp, string Text);

/// <summary>
/// Local, capped record of recent dictated text so the owner can recover a
/// dictation that pasted into the wrong place. Stored only on this machine.
/// </summary>
public sealed class DictationHistoryStore
{
    public const int MaxEntries = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;

    public DictationHistoryStore(string? pathOverride = null)
    {
        _path = pathOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "history.json");
    }

    public string StoragePath => _path;

    public IReadOnlyList<DictationHistoryEntry> LoadRecent(int limit = MaxEntries)
    {
        lock (_gate)
        {
            return ReadEntries()
                .Take(Math.Clamp(limit, 0, MaxEntries))
                .ToList();
        }
    }

    public void Append(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_gate)
        {
            var entries = ReadEntries();
            entries.Insert(0, new DictationHistoryEntry(DateTimeOffset.Now, text.Trim()));
            WriteEntries(entries.Take(MaxEntries).ToList());
        }
    }

    public int Clear()
    {
        lock (_gate)
        {
            var count = ReadEntries().Count;
            WriteEntries([]);
            return count;
        }
    }

    private List<DictationHistoryEntry> ReadEntries()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<DictationHistoryEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void WriteEntries(List<DictationHistoryEntry> entries)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(entries, JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch
        {
            // History is best-effort; never disturb dictation.
        }
    }
}
