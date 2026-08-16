using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WorkerTranscriptionEngineIntegrationTests
{
    private static readonly string WorkerPath =
        @"C:\Tools\lafazflow-whisper-worker\bin\lafazflow-whisper-worker.exe";
    private static readonly string ModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin";
    private static readonly string VadModelPath = @"C:\Models\whisper\ggml-silero-v5.1.2.bin";
    private static readonly string FixturesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LafazFlow",
        "Benchmarks",
        "fixtures-m1-2026-08-13");

    private static bool Available => File.Exists(WorkerPath)
        && File.Exists(ModelPath)
        && Directory.Exists(FixturesDirectory);

    [Fact]
    public async Task WorkerEngineTranscribesFinalizedAudio()
    {
        if (!Available)
        {
            return;
        }

        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            ModelPath = ModelPath,
            QualityModelPath = ModelPath,
            VadModelPath = VadModelPath,
            EnableVad = true,
            WhisperThreads = 16
        };
        var fixture = Directory.GetFiles(FixturesDirectory, "*.wav")
            .OrderBy(path => new FileInfo(path).Length)
            .First();
        var expectedText = Normalize(File.ReadAllText(Path.ChangeExtension(fixture, ".txt")));

        using var supervisor = new WhisperWorkerSupervisor(new WhisperWorkerSupervisorOptions
        {
            WorkerExecutablePath = WorkerPath,
            ReadinessTimeout = TimeSpan.FromSeconds(90),
            OperationTimeout = TimeSpan.FromMinutes(2),
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        });
        var engine = new WorkerTranscriptionEngine(supervisor);

        var result = await engine.TranscribeAsync(
            fixture,
            settings,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        // Whisper's VAD can shift the first-word boundary on ambiguous onsets
        // ("please" vs "at least") between builds and runs, so assert substantive
        // equivalence (edit distance ratio and ending preservation) instead of an
        // exact match on raw ASR output.
        AssertSubstantivelyEquivalent(expectedText, Normalize(result.Text));
        await supervisor.ShutdownAsync();
    }

    private static void AssertSubstantivelyEquivalent(string expected, string actual)
    {
        var distanceRatio = EditDistanceRatio(expected, actual);
        Assert.True(distanceRatio <= 0.15, $"Transcript diverged: edit distance ratio {distanceRatio:0.000}");
        var expectedWords = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actualWords = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(expectedWords.Length > 0 && actualWords.Length > 0);
        Assert.Equal(expectedWords[^1], actualWords[^1]);
    }

    private static double EditDistanceRatio(string left, string right)
    {
        if (left.Length == 0 && right.Length == 0)
        {
            return 0;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length] / (double)Math.Max(left.Length, right.Length);
    }

    private static string Normalize(string text)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"[^\p{L}\p{N}\s]",
            string.Empty);
        return string.Join(
            ' ',
            cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => word.ToLowerInvariant()));
    }
}
