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
            builder.AppendLine($"- Edit distance: `{result.NormalizedEditDistance:0.000}`");
            builder.AppendLine($"- Key terms: `{result.ActualKeyTermCount}/{result.ExpectedKeyTermCount}`");
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
