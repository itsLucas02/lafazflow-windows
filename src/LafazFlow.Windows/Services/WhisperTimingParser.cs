using System.Globalization;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public sealed record WhisperTiming(
    long? LoadMs,
    long? SampleMs,
    long? EncodeMs,
    long? DecodeMs,
    long? PromptMs,
    long? TotalMs,
    int? TokenCount,
    int? FallbackCount);

public static partial class WhisperTimingParser
{
    public static WhisperTiming Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Empty;
        }

        return new WhisperTiming(
            ParseLong(LoadTimeRegex().Match(output), "milliseconds"),
            ParseLong(SampleTimeRegex().Match(output), "milliseconds"),
            ParseLong(EncodeTimeRegex().Match(output), "milliseconds"),
            ParseLong(DecodeTimeRegex().Match(output), "milliseconds"),
            ParseLong(PromptTimeRegex().Match(output), "milliseconds"),
            ParseLong(TotalTimeRegex().Match(output), "milliseconds"),
            ParseInt(TotalTimeRegex().Match(output), "tokens"),
            ParseInt(FallbacksRegex().Match(output), "fallbacks"));
    }

    public static WhisperTiming Empty { get; } = new(null, null, null, null, null, null, null, null);

    private static long? ParseLong(Match match, string group)
    {
        return match.Success
            && double.TryParse(
                match.Groups[group].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
            ? (long)Math.Round(value, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int? ParseInt(Match match, string group)
    {
        return match.Success
            && int.TryParse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    [GeneratedRegex(@"whisper_print_timings:\s*load time\s*=\s*(?<milliseconds>[\d.]+)\s*ms", RegexOptions.IgnoreCase)]
    private static partial Regex LoadTimeRegex();

    [GeneratedRegex(@"whisper_print_timings:\s*sample time\s*=\s*(?<milliseconds>[\d.]+)\s*ms", RegexOptions.IgnoreCase)]
    private static partial Regex SampleTimeRegex();

    [GeneratedRegex(@"whisper_print_timings:\s*encode time\s*=\s*(?<milliseconds>[\d.]+)\s*ms\s*/\s*(?<runs>\d+)\s*runs", RegexOptions.IgnoreCase)]
    private static partial Regex EncodeTimeRegex();

    [GeneratedRegex(@"whisper_print_timings:\s*decode time\s*=\s*(?<milliseconds>[\d.]+)\s*ms\s*/\s*(?<runs>\d+)\s*runs", RegexOptions.IgnoreCase)]
    private static partial Regex DecodeTimeRegex();

    [GeneratedRegex(@"whisper_print_timings:\s*prompt time\s*=\s*(?<milliseconds>[\d.]+)\s*ms\s*/\s*(?<tokens>\d+)\s*tokens", RegexOptions.IgnoreCase)]
    private static partial Regex PromptTimeRegex();

    [GeneratedRegex(@"whisper_print_timings:\s*total time\s*=\s*(?<milliseconds>[\d.]+)\s*ms\s*/\s*(?<tokens>\d+)\s*tokens", RegexOptions.IgnoreCase)]
    private static partial Regex TotalTimeRegex();

    [GeneratedRegex(@"whisper_print_timings:\s*fallbacks\s*=\s*(?<fallbacks>\d+)\s*p", RegexOptions.IgnoreCase)]
    private static partial Regex FallbacksRegex();
}
