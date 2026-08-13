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
        Assert.Equal(expectedText, Normalize(result.Text));
        await supervisor.ShutdownAsync();
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
