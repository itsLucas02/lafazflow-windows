using System.Globalization;
using System.Text;

namespace LafazFlow.TranscriptionBench;

public static class BenchmarkReportWriter
{
    public static (string MarkdownPath, string CsvPath) Write(
        string outputDirectory,
        IReadOnlyList<BenchmarkResult> results,
        DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(outputDirectory);
        var stamp = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var markdownPath = Path.Combine(outputDirectory, $"lafazflow-transcription-bench-{stamp}.md");
        var csvPath = Path.Combine(outputDirectory, $"lafazflow-transcription-bench-{stamp}.csv");

        File.WriteAllText(markdownPath, BuildMarkdown(results, timestamp), Encoding.UTF8);
        File.WriteAllText(csvPath, BuildCsv(results), Encoding.UTF8);

        return (markdownPath, csvPath);
    }

    public static string WriteSummary(
        string outputDirectory,
        string label,
        IReadOnlyList<BenchmarkResult> results,
        DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(outputDirectory);
        var stamp = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safeLabel = string.IsNullOrWhiteSpace(label)
            ? "baseline"
            : new string(label.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray());
        var summaryPath = Path.Combine(outputDirectory, $"lafazflow-baseline-summary-{safeLabel}-{stamp}.md");
        File.WriteAllText(summaryPath, BuildSummaryMarkdown(label, results, timestamp), Encoding.UTF8);
        return summaryPath;
    }

    private static string BuildSummaryMarkdown(
        string label,
        IReadOnlyList<BenchmarkResult> results,
        DateTimeOffset timestamp)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# LafazFlow Baseline Summary (privacy-safe)");
        builder.AppendLine();
        builder.AppendLine($"Label: {EscapeMarkdown(string.IsNullOrWhiteSpace(label) ? "baseline" : label)}");
        builder.AppendLine($"Generated: {timestamp:O}");
        builder.AppendLine();
        builder.AppendLine("No transcript, audio, clipboard, or prompt contents are included in this summary.");
        builder.AppendLine();

        foreach (var group in results.GroupBy(result => result.ConfigName).OrderBy(group => group.Key))
        {
            var runs = group.ToArray();
            var successful = runs.Where(result => result.Succeeded).ToArray();
            var warm = successful.Where(result => !result.IsCold).Select(result => (double)result.ElapsedMilliseconds).ToArray();
            var cold = successful.Where(result => result.IsCold).Select(result => (double)result.ElapsedMilliseconds).ToArray();
            var failures = runs.Count(result => !result.Succeeded);
            var empties = successful.Count(result => result.IsEmptyResult);

            builder.AppendLine($"## {EscapeMarkdown(group.Key)}");
            builder.AppendLine();
            builder.AppendLine($"- Runs: {runs.Length} (cold: {cold.Length}, warm: {warm.Length})");
            builder.AppendLine($"- Failures: {failures}; empty results: {empties}");
            builder.AppendLine($"- Cold median: {FormatMs(Median(cold))}");
            builder.AppendLine($"- Warm median: {FormatMs(Median(warm))}; P90: {FormatMs(Percentile(warm, 0.90))}; P95: {FormatMs(Percentile(warm, 0.95))}; max: {FormatMs(Maximum(warm))}");
            builder.AppendLine($"- Model load median: {FormatMs(Median(successful.Select(result => result.ModelLoadMs)))}");
            builder.AppendLine($"- Inference median: {FormatMs(Median(successful.Select(result => result.InferenceMs)))}");
            builder.AppendLine($"- Inference RTF median: {FormatFactor(Median(successful.Select(result => result.RealtimeFactor)))}");
            builder.AppendLine($"- Mean edit distance: {MeanEditDistance(successful):0.000}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildMarkdown(IReadOnlyList<BenchmarkResult> results, DateTimeOffset timestamp)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# LafazFlow Transcription Benchmark");
        builder.AppendLine();
        builder.AppendLine($"Generated: {timestamp:O}");
        builder.AppendLine();
        builder.AppendLine("| Config | Runs | Success | Avg ms | Avg edit distance | Key terms |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");

        foreach (var group in results.GroupBy(result => result.ConfigName).OrderBy(group => group.Key))
        {
            var successful = group.Where(result => result.Succeeded).ToArray();
            var averageMs = successful.Length == 0 ? 0 : successful.Average(result => result.ElapsedMilliseconds);
            var averageDistance = successful.Length == 0 ? 1 : successful.Average(result => result.NormalizedEditDistance);
            var actualTerms = successful.Sum(result => result.ActualKeyTermCount);
            var expectedTerms = successful.Sum(result => result.ExpectedKeyTermCount);
            builder.AppendLine(
                $"| {EscapeMarkdown(group.Key)} | {group.Count()} | {successful.Length} | {averageMs:0} | {averageDistance:0.000} | {actualTerms}/{expectedTerms} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Runs");
        foreach (var result in results)
        {
            builder.AppendLine();
            builder.AppendLine($"### {EscapeMarkdown(result.FixtureId)} / {EscapeMarkdown(result.ConfigName)}");
            builder.AppendLine();
            builder.AppendLine($"- Model: `{result.ModelFileName}`");
            builder.AppendLine($"- Backend: `{result.Backend}`");
            builder.AppendLine($"- Latency: `{result.ElapsedMilliseconds} ms`");
            if (result.AudioDurationMs.HasValue)
            {
                builder.AppendLine($"- Audio duration: `{result.AudioDurationMs.Value} ms`");
            }

            if (result.ProcessStartMs.HasValue)
            {
                builder.AppendLine($"- Process start: `{result.ProcessStartMs.Value} ms`");
            }

            if (result.ModelLoadMs.HasValue)
            {
                builder.AppendLine($"- Model load: `{result.ModelLoadMs.Value} ms`");
            }

            if (result.InferenceMs.HasValue)
            {
                builder.AppendLine($"- Inference: `{result.InferenceMs.Value} ms`");
            }

            if (result.RealtimeFactor.HasValue)
            {
                builder.AppendLine($"- RTF: `{result.RealtimeFactor.Value:0.000}`");
            }

            builder.AppendLine($"- Edit distance: `{result.NormalizedEditDistance:0.000}`");
            builder.AppendLine($"- Key terms: `{result.ActualKeyTermCount}/{result.ExpectedKeyTermCount}`");
            builder.AppendLine($"- Cold run: `{result.IsCold}`; repeat: `{result.RepeatIndex}`");
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                builder.AppendLine($"- Error: `{result.Error}`");
            }

            builder.AppendLine();
            builder.AppendLine("Expected:");
            builder.AppendLine("```text");
            builder.AppendLine(result.ExpectedTranscript);
            builder.AppendLine("```");
            builder.AppendLine("Raw:");
            builder.AppendLine("```text");
            builder.AppendLine(result.RawTranscript);
            builder.AppendLine("```");
            builder.AppendLine("Post-processed:");
            builder.AppendLine("```text");
            builder.AppendLine(result.PostProcessedTranscript);
            builder.AppendLine("```");
        }

        return builder.ToString();
    }

    private static string FormatMs(double? value)
    {
        return value.HasValue ? $"{value.Value:0} ms" : "na";
    }

    private static string FormatFactor(double? value)
    {
        return value.HasValue ? $"{value.Value:0.000}" : "na";
    }

    private static double? Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static double? Median(IEnumerable<long?> values)
    {
        return Median(values.Where(value => value.HasValue).Select(value => (double)value!.Value));
    }

    private static double? Median(IEnumerable<double?> values)
    {
        return Median(values.Where(value => value.HasValue).Select(value => value!.Value));
    }

    private static double? Percentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static double? Maximum(IEnumerable<double> values)
    {
        return values.Any() ? values.Max() : null;
    }

    private static double MeanEditDistance(IEnumerable<BenchmarkResult> results)
    {
        var successful = results.ToArray();
        return successful.Length == 0 ? 1 : successful.Average(result => result.NormalizedEditDistance);
    }

    private static string BuildCsv(IReadOnlyList<BenchmarkResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("fixture_id,config,model,backend,elapsed_ms,edit_distance,key_terms,error,expected,raw,post_processed");
        foreach (var result in results)
        {
            builder.AppendLine(string.Join(
                ',',
                Csv(result.FixtureId),
                Csv(result.ConfigName),
                Csv(result.ModelFileName),
                Csv(result.Backend),
                result.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                result.NormalizedEditDistance.ToString("0.000", CultureInfo.InvariantCulture),
                Csv($"{result.ActualKeyTermCount}/{result.ExpectedKeyTermCount}"),
                Csv(result.Error ?? ""),
                Csv(result.ExpectedTranscript),
                Csv(result.RawTranscript),
                Csv(result.PostProcessedTranscript)));
        }

        return builder.ToString();
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|");
    }
}
