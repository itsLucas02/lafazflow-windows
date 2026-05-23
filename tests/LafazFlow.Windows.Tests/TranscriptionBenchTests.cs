using LafazFlow.TranscriptionBench;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Tests;

public sealed class TranscriptionBenchTests
{
    [Fact]
    public void DiscoverFixturesReturnsNewestWavFilesWithMatchingExpectedText()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var olderWav = Path.Combine(root, "older.wav");
        var newerWav = Path.Combine(root, "newer.wav");
        var missingTextWav = Path.Combine(root, "missing-text.wav");
        File.WriteAllBytes(olderWav, [1]);
        File.WriteAllText(Path.ChangeExtension(olderWav, ".txt"), "Older transcript");
        File.WriteAllBytes(newerWav, [1]);
        File.WriteAllText(Path.ChangeExtension(newerWav, ".txt"), "Newer transcript");
        File.WriteAllBytes(missingTextWav, [1]);
        File.SetLastWriteTimeUtc(olderWav, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(newerWav, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(missingTextWav, DateTime.UtcNow.AddMinutes(5));

        var fixtures = RecordingFixtureDiscovery.Discover(root, take: 10);

        Assert.Collection(
            fixtures,
            fixture =>
            {
                Assert.Equal("newer", fixture.Id);
                Assert.Equal("Newer transcript", fixture.ExpectedText);
            },
            fixture =>
            {
                Assert.Equal("older", fixture.Id);
                Assert.Equal("Older transcript", fixture.ExpectedText);
            });
    }

    [Fact]
    public void DiscoverFixturesHonorsTakeLimit()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        for (var index = 0; index < 3; index++)
        {
            var wav = Path.Combine(root, $"{index}.wav");
            File.WriteAllBytes(wav, [1]);
            File.WriteAllText(Path.ChangeExtension(wav, ".txt"), $"Transcript {index}");
            File.SetLastWriteTimeUtc(wav, DateTime.UtcNow.AddMinutes(index));
        }

        var fixtures = RecordingFixtureDiscovery.Discover(root, take: 2);

        Assert.Equal(["2", "1"], fixtures.Select(fixture => fixture.Id));
    }

    [Fact]
    public void TextMetricsComputesNormalizedEditDistanceAndKeyTerms()
    {
        var metrics = TextMetrics.Compare(
            "Open Supabase and shadcn with Context7.",
            "Open Supabaes and shadcn with contact seven.",
            ["Supabase", "shadcn", "Context7"]);

        Assert.True(metrics.NormalizedEditDistance > 0);
        Assert.True(metrics.NormalizedEditDistance < 0.5);
        Assert.Equal(3, metrics.ExpectedKeyTermCount);
        Assert.Equal(1, metrics.ActualKeyTermCount);
        Assert.Equal(["Context7", "Supabase"], metrics.MissingKeyTerms);
    }

    [Fact]
    public void BenchmarkConfigFactoryBuildsExpectedConfigsAndSkipsMissingCuda()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var cliPath = WriteTempFile(root, "whisper-cli.exe");
        var baseModelPath = WriteTempFile(root, "ggml-base.en.bin");
        var qualityModelPath = WriteTempFile(root, "ggml-large-v3-turbo-q5_0.bin");
        var vadModelPath = WriteTempFile(root, "ggml-silero-v5.1.2.bin");
        var settings = AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            CudaWhisperCliPath = Path.Combine(root, "missing-cuda", "whisper-cli.exe"),
            ModelPath = baseModelPath,
            QualityModelPath = qualityModelPath,
            VadModelPath = vadModelPath,
            WhisperThreads = 16,
            TranscriptionProfile = TranscriptionProfile.Fast,
            WhisperBackend = WhisperBackend.Cpu,
            EnableVad = true
        };

        var configs = BenchmarkConfigFactory.Build(settings, configFilter: null);

        Assert.Contains(configs, config => config.Name == "current-settings" && !config.IsSkipped);
        Assert.Contains(configs, config => config.Name == "fast-cpu-base-en" && !config.IsSkipped);
        Assert.Contains(configs, config => config.Name == "quality-cpu-q5" && !config.IsSkipped);
        Assert.Contains(configs, config => config.Name == "quality-cuda-q5-vad" && config.IsSkipped);
        var macosLike = Assert.Single(configs, config => config.Name == "macos-like-q5");
        Assert.False(macosLike.IsSkipped);
        Assert.Equal(0.2, macosLike.Runtime.DecodeOptions.Temperature);
        Assert.False(macosLike.Runtime.DecodeOptions.NoFallback);
        Assert.Equal(0, macosLike.Runtime.DecodeOptions.MaxContextTokens);
    }

    [Fact]
    public void BenchOptionsResolvesNamedRegressionPack()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var options = BenchOptions.Parse(["--pack", "daily", "--packs-root", root, "--take", "3"]);

        Assert.Equal("daily", options.PackName);
        Assert.Equal(root, options.PacksRoot);
        Assert.Equal(Path.Combine(root, "daily"), options.RecordingsDirectory);
        Assert.Equal(3, options.Take);
    }

    [Fact]
    public void BenchOptionsRejectsUnsafeRegressionPackName()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            BenchOptions.Parse(["--pack", @"..\secret"]));

        Assert.Contains("letters, numbers, dots, dashes, and underscores", exception.Message);
    }

    [Fact]
    public void BenchmarkRunnerTracksStripeAsDefaultKeyTerm()
    {
        Assert.Contains("Stripe", BenchmarkRunner.DefaultKeyTerms);
    }

    private static string WriteTempFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }
}
