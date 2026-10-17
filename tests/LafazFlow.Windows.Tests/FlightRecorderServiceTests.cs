using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class FlightRecorderServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lafazflow-ftr-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    private FlightRecorderService CreateService(int maxRecordings = FlightRecorderService.DefaultMaxRecordings, TimeSpan? maxAge = null)
        => new(_root, maxRecordings, maxAge);

    private static string WriteSourceFile(string content = "pcm")
    {
        var path = Path.Combine(Path.GetTempPath(), $"lafazflow-ftr-src-{Guid.NewGuid():N}.wav");
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteRingFile(string name, DateTime lastWriteUtc, string content = "pcm")
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    [Fact]
    public void KeepMovesFileIntoRingWithTimestampAndDictationId()
    {
        var service = CreateService();
        var source = WriteSourceFile("data");
        var id = Guid.NewGuid();

        service.Keep(source, id);

        Assert.False(File.Exists(source));
        var ringed = Assert.Single(Directory.GetFiles(_root, "*.wav"));
        var name = Path.GetFileName(ringed);
        Assert.Matches(@"^\d{8}-\d{6}-[0-9a-f]{8}\.wav$", name);
        Assert.Contains(id.ToString("N")[..8], name);
        Assert.Equal("data", File.ReadAllText(ringed));
    }

    [Fact]
    public void KeepFallsBackQuietlyWhenSourceCannotBeMoved()
    {
        var service = CreateService();

        service.Keep(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.wav"), Guid.NewGuid());

        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root, "*.wav") : []);
    }

    [Fact]
    public void PruneDeletesOldestBeyondCount()
    {
        var service = CreateService(maxRecordings: 3);
        WriteRingFile("20261017-000001-aaaa.wav", DateTime.UtcNow.AddMinutes(-5));
        WriteRingFile("20261017-000002-bbbb.wav", DateTime.UtcNow.AddMinutes(-4));
        WriteRingFile("20261017-000003-cccc.wav", DateTime.UtcNow.AddMinutes(-3));
        WriteRingFile("20261017-000004-dddd.wav", DateTime.UtcNow.AddMinutes(-2));
        WriteRingFile("20261017-000005-eeee.wav", DateTime.UtcNow.AddMinutes(-1));

        service.Prune();

        var names = Directory.GetFiles(_root).Select(Path.GetFileName).OrderBy(name => name).ToList();
        Assert.Equal(
            ["20261017-000003-cccc.wav", "20261017-000004-dddd.wav", "20261017-000005-eeee.wav"],
            names);
    }

    [Fact]
    public void PruneDeletesRecordingsOlderThanMaxAge()
    {
        var service = CreateService(maxAge: TimeSpan.FromHours(48));
        var expired = WriteRingFile("20261015-000001-aaaa.wav", DateTime.UtcNow.AddHours(-49));
        var recent = WriteRingFile("20261017-000002-bbbb.wav", DateTime.UtcNow.AddHours(-47));

        service.Prune();

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void PurgeAllRemovesEveryRecording()
    {
        var service = CreateService();
        WriteRingFile("20261017-000001-aaaa.wav", DateTime.UtcNow);
        WriteRingFile("20261017-000002-bbbb.wav", DateTime.UtcNow);

        var removed = service.PurgeAll();

        Assert.Equal(2, removed);
        Assert.Empty(Directory.GetFiles(_root, "*.wav"));
    }

    [Fact]
    public void SaveLatestCopiesNewestRecording()
    {
        var service = CreateService();
        WriteRingFile("20261017-000001-aaaa.wav", DateTime.UtcNow.AddMinutes(-2), "old");
        WriteRingFile("20261017-000002-bbbb.wav", DateTime.UtcNow.AddMinutes(-1), "new");
        var destination = Path.Combine(Path.GetTempPath(), $"lafazflow-ftr-save-{Guid.NewGuid():N}.wav");

        var saved = service.SaveLatest(destination);

        Assert.True(saved);
        Assert.Equal("new", File.ReadAllText(destination));
        File.Delete(destination);
    }

    [Fact]
    public void SaveLatestReturnsFalseWhenRingIsEmpty()
    {
        var service = CreateService();

        var saved = service.SaveLatest(Path.Combine(Path.GetTempPath(), $"lafazflow-ftr-save-{Guid.NewGuid():N}.wav"));

        Assert.False(saved);
    }

    [Fact]
    public void SnapshotReportsCountAndBounds()
    {
        var service = CreateService();
        var (emptyCount, emptyOldest, emptyNewest) = service.GetSnapshot();
        Assert.Equal(0, emptyCount);
        Assert.Null(emptyOldest);
        Assert.Null(emptyNewest);

        WriteRingFile("20261017-000001-aaaa.wav", DateTime.UtcNow.AddMinutes(-2));
        WriteRingFile("20261017-000002-bbbb.wav", DateTime.UtcNow.AddMinutes(-1));

        var (count, oldest, newest) = service.GetSnapshot();
        Assert.Equal(2, count);
        Assert.NotNull(oldest);
        Assert.NotNull(newest);
        Assert.True(oldest < newest);
    }
}
