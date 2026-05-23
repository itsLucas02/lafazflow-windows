using System.Diagnostics;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.TranscriptionBench;

public sealed class BenchmarkRunner
{
    public static readonly string[] DefaultKeyTerms =
    [
        "Supabase",
        "shadcn",
        "Context7",
        "Luqman",
        "MediBrave",
        "Stripe",
        "stale",
        "wrapper",
        "align"
    ];

    private readonly ITranscriptionService _transcription;

    public BenchmarkRunner(ITranscriptionService? transcription = null)
    {
        _transcription = transcription ?? new WhisperCliTranscriptionService();
    }

    public async Task<IReadOnlyList<BenchmarkResult>> RunAsync(
        IReadOnlyList<RecordingFixture> fixtures,
        IReadOnlyList<BenchmarkTranscriptionConfig> configs,
        CancellationToken cancellationToken)
    {
        var results = new List<BenchmarkResult>();
        foreach (var fixture in fixtures)
        {
            foreach (var config in configs)
            {
                results.Add(await RunOneAsync(fixture, config, cancellationToken));
            }
        }

        return results;
    }

    private async Task<BenchmarkResult> RunOneAsync(
        RecordingFixture fixture,
        BenchmarkTranscriptionConfig config,
        CancellationToken cancellationToken)
    {
        var modelFileName = Path.GetFileName(config.Runtime.ModelPath);
        var backend = config.Settings.TranscriptionProfile == TranscriptionProfile.Quality
            ? config.Settings.WhisperBackend.ToString()
            : WhisperBackend.Cpu.ToString();

        if (config.IsSkipped)
        {
            return CreateResult(fixture, config, modelFileName, backend, 0, "", "", config.SkipReason);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(config.Settings);
            var raw = await _transcription.TranscribeAsync(
                config.Runtime.CliPath,
                config.Runtime.ModelPath,
                fixture.AudioPath,
                prompt,
                config.Settings.WhisperThreads,
                config.Runtime.DecodeOptions,
                cancellationToken);
            stopwatch.Stop();
            var postProcessed = raw;
            if (config.Settings.EnableVocabularyCorrections)
            {
                postProcessed = VocabularyCorrectionService.Apply(postProcessed, config.Settings.CustomCorrectionRules);
            }

            return CreateResult(fixture, config, modelFileName, backend, stopwatch.ElapsedMilliseconds, raw, postProcessed, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateResult(fixture, config, modelFileName, backend, stopwatch.ElapsedMilliseconds, "", "", ex.Message);
        }
    }

    private static BenchmarkResult CreateResult(
        RecordingFixture fixture,
        BenchmarkTranscriptionConfig config,
        string modelFileName,
        string backend,
        long elapsedMilliseconds,
        string rawTranscript,
        string postProcessedTranscript,
        string? error)
    {
        var metrics = TextMetrics.Compare(fixture.ExpectedText, postProcessedTranscript, DefaultKeyTerms);
        return new BenchmarkResult(
            fixture.Id,
            config.Name,
            modelFileName,
            backend,
            elapsedMilliseconds,
            fixture.ExpectedText,
            rawTranscript,
            postProcessedTranscript,
            metrics.NormalizedEditDistance,
            metrics.ExpectedKeyTermCount,
            metrics.ActualKeyTermCount,
            metrics.MissingKeyTerms,
            error);
    }
}
