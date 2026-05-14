using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public static partial class TranscriptionTextFormatter
{
    public static string Format(string text)
    {
        var withoutTimestamps = TimestampLineRegex().Replace(text, " ");
        var normalized = WhitespaceRegex().Replace(withoutTimestamps, " ").Trim();
        normalized = SpaceBeforePunctuationRegex().Replace(normalized, "$1");

        if (normalized.Length == 0)
        {
            return normalized;
        }

        normalized = char.ToUpperInvariant(normalized[0]) + normalized[1..];

        if (!EndsWithSentencePunctuation(normalized))
        {
            normalized += ".";
        }

        return normalized;
    }

    private static bool EndsWithSentencePunctuation(string text)
    {
        var last = text[^1];
        return last is '.' or '?' or '!';
    }

    [GeneratedRegex(@"\[[0-9:.]+\s*-->\s*[0-9:.]+\]")]
    private static partial Regex TimestampLineRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+([,.;:!?])")]
    private static partial Regex SpaceBeforePunctuationRegex();
}
