using System.Diagnostics;
using System.Text.Json;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;
using LafazFlow.WorkerVerification;

var options = VerifyOptions.Parse(args);
var cliBaseline = CliBaselineLoader.Load(options.CliBaselinePath);
var settings = VerifySettingsLoader.Load(options.SettingsPath) with
{
    TranscriptionProfile = TranscriptionProfile.Quality,
    WhisperBackend = options.Backend.Equals("Cpu", StringComparison.OrdinalIgnoreCase)
        ? WhisperBackend.Cpu
        : WhisperBackend.Cuda,
    ModelPath = options.ModelPath,
    QualityModelPath = options.ModelPath,
    VadModelPath = options.VadModelPath,
    EnableVad = true,
    WhisperThreads = options.Threads
};

var fixtures = Directory
    .GetFiles(options.FixturesDirectory, "*.wav")
    .OrderBy(path => new FileInfo(path).Length)
    .ToArray();
if (fixtures.Length == 0)
{
    Console.Error.WriteLine($"No .wav fixtures found in {options.FixturesDirectory}.");
    return 2;
}

if (!File.Exists(options.WorkerPath))
{
    Console.Error.WriteLine($"Worker not found: {options.WorkerPath}");
    return 2;
}

using var supervisor = new WhisperWorkerSupervisor(new WhisperWorkerSupervisorOptions
{
    WorkerExecutablePath = options.WorkerPath,
    ReadinessTimeout = TimeSpan.FromSeconds(120),
    OperationTimeout = TimeSpan.FromMinutes(2),
    ShutdownTimeout = TimeSpan.FromSeconds(5)
});
var engine = new WorkerTranscriptionEngine(supervisor);

var loadStopwatch = Stopwatch.StartNew();
var session = await supervisor.GetReadySessionAsync(settings, CancellationToken.None);
loadStopwatch.Stop();

// Phase 1: initial model-load/readiness allocation (captured before warmup).
var (workingSetAfterReady, vramAfterReadyMiB) = await CaptureMemoryAsync(session.ProcessId);

var warmups = Math.Max(1, options.Warmup);
for (var index = 0; index < warmups; index++)
{
    var fixturePath = fixtures[index % fixtures.Length];
    var warmupResult = await engine.TranscribeAsync(fixturePath, settings, Guid.NewGuid(), CancellationToken.None);
    if (!warmupResult.Succeeded)
    {
        Console.Error.WriteLine($"Warmup request failed: {warmupResult.FailureKind}");
        return 3;
    }
}

// Phase 2: post-warmup baseline. Baselines are captured only after warmup so
// first-inference native/CUDA allocation is excluded from steady-state growth.
var (workingSetAfterWarmup, vramAfterWarmupMiB) = await CaptureMemoryAsync(session.ProcessId);

var samples = new List<RequestSample>();
var checkpoints = new List<MemoryCheckpoint>();
var measuredTotal = fixtures.Length * options.Repeats;
var checkpointInterval = Math.Max(1, options.CheckpointInterval);
for (var index = 0; index < measuredTotal; index++)
{
    var fixturePath = fixtures[index % fixtures.Length];
    var expectedPath = Path.ChangeExtension(fixturePath, ".txt");
    var expected = File.Exists(expectedPath) ? File.ReadAllText(expectedPath) : "";
    var compared = expected.Length > 0;
    var fixtureId = Path.GetFileNameWithoutExtension(fixturePath);
    cliBaseline.TryGetValue(fixtureId, out var cliRaw);

    var requestStopwatch = Stopwatch.StartNew();
    var result = await engine.TranscribeAsync(fixturePath, settings, Guid.NewGuid(), CancellationToken.None);
    requestStopwatch.Stop();

    var normalizedResult = Normalize(result.Text);
    var normalizedExpected = Normalize(expected);
    var editDistanceRatio = normalizedExpected.Length == 0
        ? (normalizedResult.Length == 0 ? 0 : 1.0)
        : EditDistance(normalizedResult, normalizedExpected)
            / Math.Max(normalizedResult.Length, normalizedExpected.Length);
    samples.Add(new RequestSample(
        fixtureId,
        requestStopwatch.ElapsedMilliseconds,
        result.Succeeded,
        string.IsNullOrWhiteSpace(result.Text),
        compared && normalizedResult == normalizedExpected,
        compared,
        !string.IsNullOrWhiteSpace(cliRaw) && normalizedResult == Normalize(cliRaw),
        !string.IsNullOrWhiteSpace(cliRaw),
        WordRecall(normalizedResult, normalizedExpected),
        editDistanceRatio,
        compared && EndingPreserved(normalizedResult, normalizedExpected),
        WavFileValidator.Inspect(fixturePath)?.DurationMilliseconds));

    if (Environment.GetEnvironmentVariable("LAFAZFLOW_VERIFY_DEBUG") == "1")
    {
        Console.WriteLine(
            $"DEBUG {Path.GetFileName(fixturePath)} ok={result.Succeeded} " +
            $"match={compared && normalizedResult == normalizedExpected} " +
            $"edit={editDistanceRatio:0.000} " +
            $"recall={WordRecall(normalizedResult, normalizedExpected):0.00} " +
            $"raw=[{result.Text}]");
    }

    // Phase 3: memory checkpoints during the run, not only at the end.
    var measuredCount = index + 1;
    if (measuredCount % checkpointInterval == 0 || measuredCount == measuredTotal)
    {
        var (checkpointWorkingSet, checkpointVram) = await CaptureMemoryAsync(session.ProcessId);
        checkpoints.Add(new MemoryCheckpoint(measuredCount, checkpointWorkingSet, checkpointVram));
    }
}

var finalCheckpoint = checkpoints[^1];
var workingSetAfter = finalCheckpoint.WorkingSetBytes;
var vramAfterMiB = finalCheckpoint.VramMiB;

await supervisor.ShutdownAsync();
var orphan = TryGetWorkerAlive(session.ProcessId);

var stability = MemoryStabilityAnalyzer.Classify(
    workingSetAfterWarmup,
    workingSetAfter,
    vramAfterWarmupMiB,
    vramAfterMiB,
    options.WorkingSetToleranceBytes,
    options.VramToleranceMiB,
    checkpoints);

var summary = BuildSummary(
    options,
    settings,
    samples,
    loadStopwatch.ElapsedMilliseconds,
    workingSetAfterReady,
    vramAfterReadyMiB,
    workingSetAfterWarmup,
    vramAfterWarmupMiB,
    workingSetAfter,
    vramAfterMiB,
    checkpoints,
    stability,
    orphan is null);
Directory.CreateDirectory(options.OutputDirectory);
var summaryPath = Path.Combine(
    options.OutputDirectory,
    $"lafazflow-worker-verify-{DateTime.Now:yyyyMMdd-HHmmss}.json");
await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Worker verification summary: {summaryPath}");
Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine();
PrintMemoryReport(
    workingSetAfterReady,
    vramAfterReadyMiB,
    workingSetAfterWarmup,
    vramAfterWarmupMiB,
    checkpoints,
    stability);

var allSucceeded = samples.Count > 0 && samples.All(sample => sample.Succeeded);
return allSucceeded
    && orphan is null
    && MemoryStabilityAnalyzer.PassesVerificationGate(stability.Verdict)
    ? 0
    : 1;

static async Task<(long WorkingSetBytes, long? VramMiB)> CaptureMemoryAsync(int processId)
{
    try
    {
        using var workerProcess = Process.GetProcessById(processId);
        workerProcess.Refresh();
        var vram = await NvidiaSmiUsedMiBAsync();
        return (workerProcess.WorkingSet64, vram);
    }
    catch
    {
        return (0, null);
    }
}

static void PrintMemoryReport(
    long workingSetAfterReady,
    long? vramAfterReadyMiB,
    long workingSetAfterWarmup,
    long? vramAfterWarmupMiB,
    IReadOnlyList<MemoryCheckpoint> checkpoints,
    MemoryStabilityResult stability)
{
    Console.WriteLine("Memory report (privacy-safe):");
    Console.WriteLine($"  Initial model-load/readiness: working set {workingSetAfterReady} bytes, VRAM {vramAfterReadyMiB?.ToString() ?? "n/a"} MiB");
    Console.WriteLine($"  After warmup (steady-state baseline): working set {workingSetAfterWarmup} bytes, VRAM {vramAfterWarmupMiB?.ToString() ?? "n/a"} MiB");
    Console.WriteLine($"  Warmup allocation: working set {workingSetAfterWarmup - workingSetAfterReady} bytes, VRAM {WarmupVramDelta(vramAfterReadyMiB, vramAfterWarmupMiB)} MiB");
    foreach (var checkpoint in checkpoints)
    {
        Console.WriteLine($"  Checkpoint @{checkpoint.RequestIndex}: working set {checkpoint.WorkingSetBytes} bytes, VRAM {checkpoint.VramMiB?.ToString() ?? "n/a"} MiB");
    }

    Console.WriteLine($"  Post-warmup working-set growth: {stability.WorkingSetGrowthBytes} bytes");
    Console.WriteLine($"  Post-warmup VRAM growth: {stability.VramGrowthMiB?.ToString() ?? "n/a"} MiB");
    Console.WriteLine($"  Stability verdict: {stability.Verdict} — {stability.Reason}");
}

static long WarmupVramDelta(long? before, long? after)
{
    return before.HasValue && after.HasValue ? after.Value - before.Value : 0;
}

static string Normalize(string text)
{
    return string.Join(
        " ",
        (System.Text.RegularExpressions.Regex
            .Replace(text ?? "", "[^\\p{L}\\p{N}\\s]", "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Select(word => word.ToLowerInvariant()));
}

static double WordRecall(string normalizedResult, string normalizedExpected)
{
    var expectedWords = normalizedExpected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (expectedWords.Length == 0)
    {
        return 1.0;
    }

    var resultWords = normalizedResult
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet(StringComparer.Ordinal);
    var found = expectedWords.Count(resultWords.Contains);
    return (double)found / expectedWords.Length;
}

static double EditDistance(string left, string right)
{
    if (left.Length == 0)
    {
        return right.Length;
    }

    if (right.Length == 0)
    {
        return left.Length;
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

    return previous[right.Length];
}

static bool EndingPreserved(string normalizedResult, string normalizedExpected)
{
    var expectedWords = normalizedExpected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var resultWords = normalizedResult.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return expectedWords.Length > 0
        && resultWords.Length > 0
        && string.Equals(resultWords[^1], expectedWords[^1], StringComparison.Ordinal);
}

static async Task<long?> NvidiaSmiUsedMiBAsync()
{
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            Arguments = "--query-gpu=memory.used --format=csv,noheader,nounits",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return long.TryParse(firstLine?.Trim(), out var value) ? value : null;
    }
    catch
    {
        return null;
    }
}

static Process? TryGetWorkerAlive(int processId)
{
    try
    {
        var process = Process.GetProcessById(processId);
        return process.HasExited ? null : process;
    }
    catch
    {
        return null;
    }
}

static object BuildSummary(
    VerifyOptions options,
    AppSettings settings,
    IReadOnlyList<RequestSample> samples,
    long loadMs,
    long workingSetAfterReady,
    long? vramAfterReadyMiB,
    long workingSetAfterWarmup,
    long? vramAfterWarmupMiB,
    long workingSetAfter,
    long? vramAfterMiB,
    IReadOnlyList<MemoryCheckpoint> checkpoints,
    MemoryStabilityResult stability,
    bool noOrphanProcess)
{
    var successful = samples.Where(sample => sample.Succeeded).Select(sample => sample.WallMs).OrderBy(value => value).ToArray();
    var durations = samples
        .Where(sample => sample.Succeeded && sample.AudioDurationMs is > 0)
        .Select(sample => (double)sample.WallMs / sample.AudioDurationMs!.Value)
        .OrderBy(value => value)
        .ToArray();
    return new
    {
        label = options.Label,
        timestamp = DateTimeOffset.Now,
        worker = Path.GetFileName(options.WorkerPath),
        model = Path.GetFileName(settings.QualityModelPath),
        backend = options.Backend,
        vad = true,
        threads = settings.WhisperThreads,
        readiness_ms = loadMs,
        total_requests = samples.Count,
        warmup_requests = Math.Max(1, options.Warmup),
        successes = successful.Length,
        failures = samples.Count(sample => !sample.Succeeded),
        empties = samples.Count(sample => sample.Empty),
        text_matches = samples.Count(sample => sample.TextMatches),
        text_comparisons = samples.Count(sample => sample.TextCompared),
        cli_matches = samples.Count(sample => sample.CliMatched),
        cli_comparisons = samples.Count(sample => sample.CliCompared),
        mean_word_recall = samples.Count == 0 ? 0 : samples.Average(sample => sample.WordRecall),
        mean_edit_distance = samples.Count == 0 ? 0 : samples.Average(sample => sample.EditDistance),
        endings_preserved = samples.Count(sample => sample.EndingPreserved),
        endings_compared = samples.Count(sample => sample.TextCompared),
        wall_ms_median = Percentile(successful, 0.50),
        wall_ms_p90 = Percentile(successful, 0.90),
        wall_ms_p95 = Percentile(successful, 0.95),
        wall_ms_max = successful.Length == 0 ? 0 : successful[^1],
        rtf_median = durations.Length == 0 ? 0 : durations[durations.Length / 2],
        working_set_after_ready_bytes = workingSetAfterReady,
        vram_after_ready_mib = vramAfterReadyMiB,
        working_set_after_warmup_bytes = workingSetAfterWarmup,
        vram_after_warmup_mib = vramAfterWarmupMiB,
        warmup_working_set_allocation_bytes = workingSetAfterWarmup - workingSetAfterReady,
        warmup_vram_allocation_mib = vramAfterReadyMiB.HasValue && vramAfterWarmupMiB.HasValue
            ? vramAfterWarmupMiB.Value - vramAfterReadyMiB.Value
            : (long?)null,
        working_set_after_bytes = workingSetAfter,
        working_set_checkpoints = checkpoints.Select(checkpoint => new
        {
            request_index = checkpoint.RequestIndex,
            working_set_bytes = checkpoint.WorkingSetBytes,
            vram_mib = checkpoint.VramMiB
        }),
        vram_after_mib = vramAfterMiB,
        post_warmup_working_set_growth_bytes = stability.WorkingSetGrowthBytes,
        post_warmup_vram_growth_mib = stability.VramGrowthMiB,
        memory_stability = new
        {
            verdict = stability.Verdict.ToString(),
            reason = stability.Reason,
            working_set_tolerance_bytes = options.WorkingSetToleranceBytes,
            vram_tolerance_mib = options.VramToleranceMiB
        },
        orphan_process_left = !noOrphanProcess
    };
}

static long Percentile(IReadOnlyList<long> orderedValues, double percentile)
{
    if (orderedValues.Count == 0)
    {
        return 0;
    }

    var rank = (int)Math.Ceiling(percentile * orderedValues.Count) - 1;
    return orderedValues[Math.Clamp(rank, 0, orderedValues.Count - 1)];
}

internal sealed record RequestSample(
    string FixtureId,
    long WallMs,
    bool Succeeded,
    bool Empty,
    bool TextMatches,
    bool TextCompared,
    bool CliMatched,
    bool CliCompared,
    double WordRecall,
    double EditDistance,
    bool EndingPreserved,
    long? AudioDurationMs);

internal sealed record VerifyOptions(
    string WorkerPath,
    string ModelPath,
    string VadModelPath,
    int Threads,
    string FixturesDirectory,
    int Repeats,
    int Warmup,
    string SettingsPath,
    string CliBaselinePath,
    string Backend,
    int CheckpointInterval,
    long WorkingSetToleranceBytes,
    long VramToleranceMiB,
    string OutputDirectory,
    string Label)
{
    public static VerifyOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[index][2..];
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = args[++index];
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new VerifyOptions(
            values.GetValueOrDefault("worker") ?? @"C:\Tools\lafazflow-whisper-worker\bin\lafazflow-whisper-worker.exe",
            values.GetValueOrDefault("model") ?? @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            values.GetValueOrDefault("vad-model") ?? @"C:\Models\whisper\ggml-silero-v5.1.2.bin",
            int.TryParse(values.GetValueOrDefault("threads"), out var threads) ? Math.Max(1, threads) : 16,
            values.GetValueOrDefault("fixtures")
                ?? Path.Combine(localAppData, "LafazFlow", "Benchmarks", "fixtures-m1-2026-08-13"),
            int.TryParse(values.GetValueOrDefault("repeats"), out var repeats) ? Math.Max(1, repeats) : 25,
            int.TryParse(values.GetValueOrDefault("warmup"), out var warmup) ? Math.Max(1, warmup) : 2,
            values.GetValueOrDefault("settings")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LafazFlow", "settings.json"),
            values.GetValueOrDefault("cli-baseline")
                ?? Path.Combine(localAppData, "LafazFlow", "Benchmarks", "lafazflow-transcription-bench-20260813-214637.csv"),
            values.GetValueOrDefault("backend") ?? "Cuda",
            int.TryParse(values.GetValueOrDefault("checkpoint-interval"), out var checkpointInterval)
                ? Math.Max(1, checkpointInterval)
                : 25,
            (int.TryParse(values.GetValueOrDefault("ws-tolerance-mib"), out var wsTolerance)
                ? Math.Max(1, wsTolerance)
                : 64) * 1024L * 1024L,
            int.TryParse(values.GetValueOrDefault("vram-tolerance-mib"), out var vramTolerance)
                ? Math.Max(1, vramTolerance)
                : 64,
            values.GetValueOrDefault("out") ?? Path.Combine(localAppData, "LafazFlow", "Benchmarks"),
            values.GetValueOrDefault("label") ?? "");
    }
}

internal static class CliBaselineLoader
{
    public static Dictionary<string, string> Load(string csvPath)
    {
        var baseline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(csvPath))
        {
            return baseline;
        }

        try
        {
            var rows = ParseCsv(File.ReadAllText(csvPath));
            if (rows.Count == 0)
            {
                return baseline;
            }

            var header = rows[0];
            var fixtureIndex = header.FindIndex(value => value.Equals("fixture_id", StringComparison.OrdinalIgnoreCase));
            var rawIndex = header.FindIndex(value => value.Equals("raw", StringComparison.OrdinalIgnoreCase));
            if (fixtureIndex < 0 || rawIndex < 0)
            {
                return baseline;
            }

            foreach (var row in rows.Skip(1))
            {
                if (row.Count <= Math.Max(fixtureIndex, rawIndex))
                {
                    continue;
                }

                var fixtureId = row[fixtureIndex].Trim();
                var raw = row[rawIndex].Trim();
                if (fixtureId.Length > 0 && raw.Length > 0 && !baseline.ContainsKey(fixtureId))
                {
                    baseline[fixtureId] = raw;
                }
            }
        }
        catch
        {
        }

        return baseline;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if ((character == '\r' || character == '\n') && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                rows.Add(fields);
                fields = [];
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString());
        rows.Add(fields);
        return rows
            .Where(row => row.Any(field => field.Length > 0))
            .ToList();
    }
}

internal static class VerifySettingsLoader
{
    public static AppSettings Load(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return AppSettings.Default;
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? AppSettings.Default;
    }
}
