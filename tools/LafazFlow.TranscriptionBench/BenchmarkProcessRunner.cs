using System.Diagnostics;
using System.IO;
using LafazFlow.Windows.Services;

namespace LafazFlow.TranscriptionBench;

public sealed class BenchmarkProcessRunner
{
    public async Task<IReadOnlyList<BenchmarkResult>> RunAsync(
        IReadOnlyList<RecordingFixture> fixtures,
        IReadOnlyList<BenchmarkTranscriptionConfig> configs,
        int repeats,
        CancellationToken cancellationToken)
    {
        var results = new List<BenchmarkResult>();
        foreach (var fixture in fixtures)
        {
            foreach (var config in configs)
            {
                for (var repeatIndex = 0; repeatIndex < repeats; repeatIndex++)
                {
                    results.Add(await RunOneAsync(fixture, config, repeatIndex, repeatIndex == 0, cancellationToken));
                }
            }
        }

        return results;
    }

    private async Task<BenchmarkResult> RunOneAsync(
        RecordingFixture fixture,
        BenchmarkTranscriptionConfig config,
        int repeatIndex,
        bool isCold,
        CancellationToken cancellationToken)
    {
        var modelFileName = Path.GetFileName(config.Runtime.ModelPath);
        var backend = config.Settings.TranscriptionProfile == LafazFlow.Windows.Core.TranscriptionProfile.Quality
            ? config.Settings.WhisperBackend.ToString()
            : LafazFlow.Windows.Core.WhisperBackend.Cpu.ToString();

        if (config.IsSkipped)
        {
            return CreateResult(
                fixture,
                config,
                modelFileName,
                backend,
                elapsedMilliseconds: 0,
                rawTranscript: "",
                postProcessedTranscript: "",
                error: config.SkipReason,
                audioDurationMs: 0,
                processStartMs: 0,
                modelLoadMs: null,
                inferenceMs: null,
                outputReadMs: null,
                realtimeFactor: null,
                rawCharCount: null,
                formattedCharCount: null,
                rawFinalCharCategory: "",
                formattedFinalCharCategory: "",
                isCold: isCold,
                repeatIndex: repeatIndex);
        }

        var audioDurationMs = WavDurationReader.ReadMilliseconds(fixture.AudioPath);
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "LafazFlowBench",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var tempAudioPath = Path.Combine(tempDirectory, "input.wav");
        var outputBasePath = Path.Combine(tempDirectory, "input");
        File.Copy(fixture.AudioPath, tempAudioPath);

        var totalStopwatch = Stopwatch.StartNew();
        var startStopwatch = Stopwatch.StartNew();
        try
        {
            var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(config.Settings);
            var startInfo = new ProcessStartInfo
            {
                FileName = config.Runtime.CliPath,
                Arguments = WhisperCliTranscriptionService.BuildArguments(
                    config.Runtime.ModelPath,
                    tempAudioPath,
                    outputBasePath,
                    prompt,
                    config.Settings.WhisperThreads,
                    config.Runtime.DecodeOptions),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(config.Runtime.CliPath) ?? Environment.CurrentDirectory
            };
            startInfo.Environment["PATH"] = WhisperCliTranscriptionService.BuildProcessPath(
                config.Runtime.CliPath,
                Environment.GetEnvironmentVariable("PATH") ?? "");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start whisper CLI.");
            startStopwatch.Stop();
            var processStartMs = startStopwatch.ElapsedMilliseconds;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;
            var stdout = await stdoutTask;
            var timing = WhisperTimingParser.Parse(stderr);

            var outputReadStopwatch = Stopwatch.StartNew();
            var textPath = outputBasePath + ".txt";
            var raw = CleanTranscript(
                File.Exists(textPath)
                    ? await File.ReadAllTextAsync(textPath, cancellationToken)
                    : stdout);
            var outputReadMs = outputReadStopwatch.ElapsedMilliseconds;
            totalStopwatch.Stop();

            if (process.ExitCode != 0)
            {
                return CreateResult(
                    fixture,
                    config,
                    modelFileName,
                    backend,
                    totalStopwatch.ElapsedMilliseconds,
                    "",
                    "",
                    WhisperCliTranscriptionService.BuildFailureMessage(process.ExitCode, stdout, stderr),
                    audioDurationMs ?? 0,
                    processStartMs,
                    timing.LoadMs,
                    InferenceMs(timing),
                    outputReadMs,
                    RealtimeFactor(timing, audioDurationMs),
                    null,
                    null,
                    "",
                    "",
                    isCold,
                    repeatIndex);
            }

            var postProcessed = raw;
            if (config.Settings.EnableVocabularyCorrections)
            {
                postProcessed = VocabularyCorrectionService.Apply(postProcessed, config.Settings.CustomCorrectionRules);
            }

            var metrics = TextMetrics.Compare(fixture.ExpectedText, postProcessed, BenchmarkRunner.DefaultKeyTerms);
            return CreateResult(
                fixture,
                config,
                modelFileName,
                backend,
                totalStopwatch.ElapsedMilliseconds,
                raw,
                postProcessed,
                null,
                audioDurationMs ?? 0,
                processStartMs,
                timing.LoadMs,
                InferenceMs(timing),
                outputReadMs,
                RealtimeFactor(timing, audioDurationMs),
                LafazFlow.Windows.Services.TextCharMetrics.CharacterCount(raw),
                LafazFlow.Windows.Services.TextCharMetrics.CharacterCount(postProcessed),
                LafazFlow.Windows.Services.TextCharMetrics.FinalCharCategory(raw),
                LafazFlow.Windows.Services.TextCharMetrics.FinalCharCategory(postProcessed),
                isCold,
                repeatIndex,
                metrics.NormalizedEditDistance,
                metrics.ExpectedKeyTermCount,
                metrics.ActualKeyTermCount,
                metrics.MissingKeyTerms);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            totalStopwatch.Stop();
            return CreateResult(
                fixture,
                config,
                modelFileName,
                backend,
                totalStopwatch.ElapsedMilliseconds,
                "",
                "",
                ex.Message,
                audioDurationMs ?? 0,
                startStopwatch.ElapsedMilliseconds,
                null,
                null,
                null,
                null,
                null,
                null,
                "",
                "",
                isCold,
                repeatIndex);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static long? InferenceMs(WhisperTiming timing)
    {
        return timing.EncodeMs.HasValue || timing.DecodeMs.HasValue
            ? (timing.EncodeMs ?? 0) + (timing.DecodeMs ?? 0)
            : timing.TotalMs;
    }

    private static double? RealtimeFactor(WhisperTiming timing, long? audioDurationMs)
    {
        var inferenceMs = InferenceMs(timing);
        return audioDurationMs is > 0 && inferenceMs.HasValue
            ? (double)inferenceMs.Value / audioDurationMs.Value
            : null;
    }

    private static string CleanTranscript(string text)
    {
        return WhisperCliTranscriptionService.CleanTranscript(text);
    }

    private static BenchmarkResult CreateResult(
        RecordingFixture fixture,
        BenchmarkTranscriptionConfig config,
        string modelFileName,
        string backend,
        long elapsedMilliseconds,
        string rawTranscript,
        string postProcessedTranscript,
        string? error,
        long audioDurationMs,
        long processStartMs,
        long? modelLoadMs,
        long? inferenceMs,
        long? outputReadMs,
        double? realtimeFactor,
        int? rawCharCount,
        int? formattedCharCount,
        string rawFinalCharCategory,
        string formattedFinalCharCategory,
        bool isCold,
        int repeatIndex,
        double normalizedEditDistance = 1,
        int expectedKeyTermCount = 0,
        int actualKeyTermCount = 0,
        IReadOnlyList<string>? missingKeyTerms = null)
    {
        return new BenchmarkResult(
            fixture.Id,
            config.Name,
            modelFileName,
            backend,
            elapsedMilliseconds,
            fixture.ExpectedText,
            rawTranscript,
            postProcessedTranscript,
            normalizedEditDistance,
            expectedKeyTermCount,
            actualKeyTermCount,
            missingKeyTerms ?? [],
            error,
            audioDurationMs,
            processStartMs,
            modelLoadMs,
            inferenceMs,
            outputReadMs,
            realtimeFactor,
            rawCharCount,
            formattedCharCount,
            rawFinalCharCategory,
            formattedFinalCharCategory,
            isCold,
            repeatIndex);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
