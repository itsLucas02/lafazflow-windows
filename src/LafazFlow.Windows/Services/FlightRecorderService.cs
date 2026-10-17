using System.IO;

namespace LafazFlow.Windows.Services;

/// <summary>
/// Local auto-expiring ring buffer of finalized dictation WAVs so intermittent
/// transcription problems stay replayable. Never stores transcript text and
/// never leaves the machine.
/// </summary>
public sealed class FlightRecorderService
{
    public const int DefaultMaxRecordings = 50;
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(48);

    private readonly object _gate = new();
    private readonly int _maxRecordings;
    private readonly TimeSpan _maxAge;

    public FlightRecorderService(
        string? rootOverride = null,
        int maxRecordings = DefaultMaxRecordings,
        TimeSpan? maxAge = null)
    {
        Root = rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "FlightRecorder");
        _maxRecordings = maxRecordings;
        _maxAge = maxAge ?? DefaultMaxAge;
    }

    public string Root { get; }

    /// <summary>
    /// Moves a finalized dictation WAV into the ring buffer and prunes expired
    /// recordings. Never throws into the dictation path; on failure it falls
    /// back to the previous delete behavior.
    /// </summary>
    public void Keep(string wavPath, Guid dictationId)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Root);
                var target = Path.Combine(
                    Root,
                    $"{DateTime.Now:yyyyMMdd-HHmmss}-{dictationId.ToString("N")[..8]}.wav");
                File.Move(wavPath, target);
                PruneCore();
            }
        }
        catch
        {
            TryDeleteQuietly(wavPath);
        }
    }

    public void Prune()
    {
        lock (_gate)
        {
            PruneCore();
        }
    }

    public (int Count, DateTime? OldestLocal, DateTime? NewestLocal) GetSnapshot()
    {
        lock (_gate)
        {
            var files = ListRecordings();
            return files.Count == 0
                ? (0, null, null)
                : (files.Count, files[0].LastWriteTime, files[^1].LastWriteTime);
        }
    }

    public int PurgeAll()
    {
        lock (_gate)
        {
            var files = ListRecordings();
            foreach (var file in files)
            {
                TryDeleteQuietly(file.FullName);
            }

            return files.Count;
        }
    }

    public bool SaveLatest(string destinationPath)
    {
        lock (_gate)
        {
            var files = ListRecordings();
            if (files.Count == 0)
            {
                return false;
            }

            File.Copy(files[^1].FullName, destinationPath, overwrite: true);
            return true;
        }
    }

    private void PruneCore()
    {
        var files = ListRecordings();
        if (files.Count == 0)
        {
            return;
        }

        var cutoffUtc = DateTime.UtcNow - _maxAge;
        foreach (var file in files.Where(file => file.LastWriteTimeUtc < cutoffUtc).ToList())
        {
            TryDeleteQuietly(file.FullName);
            files.Remove(file);
        }

        while (files.Count > _maxRecordings)
        {
            var oldest = files[0];
            files.RemoveAt(0);
            TryDeleteQuietly(oldest.FullName);
        }
    }

    private List<FileInfo> ListRecordings()
    {
        try
        {
            if (!Directory.Exists(Root))
            {
                return [];
            }

            return Directory.GetFiles(Root, "*.wav")
                .Select(path => new FileInfo(path))
                .Where(info => info.Exists)
                .OrderBy(info => info.Name, StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
