namespace LafazFlow.TranscriptionBench;

public static class RecordingFixtureDiscovery
{
    public static IReadOnlyList<RecordingFixture> Discover(string recordingsDirectory, int take)
    {
        if (!Directory.Exists(recordingsDirectory) || take <= 0)
        {
            return [];
        }

        return Directory
            .EnumerateFiles(recordingsDirectory, "*.wav", SearchOption.TopDirectoryOnly)
            .Select(CreateFixture)
            .Where(fixture => fixture is not null)
            .Cast<RecordingFixture>()
            .OrderByDescending(fixture => fixture.LastWriteTimeUtc)
            .ThenBy(fixture => fixture.Id, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToArray();
    }

    private static RecordingFixture? CreateFixture(string audioPath)
    {
        var expectedTextPath = Path.ChangeExtension(audioPath, ".txt");
        if (!File.Exists(expectedTextPath))
        {
            return null;
        }

        return new RecordingFixture(
            Path.GetFileNameWithoutExtension(audioPath),
            audioPath,
            File.ReadAllText(expectedTextPath).Trim(),
            File.GetLastWriteTimeUtc(audioPath));
    }
}
