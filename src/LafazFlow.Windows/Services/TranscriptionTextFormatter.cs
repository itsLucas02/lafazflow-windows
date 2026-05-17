using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public static partial class TranscriptionTextFormatter
{
    private static readonly string[] QuestionStarters =
    [
        "why",
        "what",
        "how",
        "when",
        "where",
        "who",
        "can",
        "could",
        "would",
        "should",
        "do",
        "does",
        "did",
        "is",
        "are",
        "am"
    ];

    public static string Format(string text)
    {
        var withoutTimestamps = TimestampLineRegex().Replace(text, " ");
        var withoutAudioMarkers = NonSpeechMarkerRegex().Replace(withoutTimestamps, " ");
        var normalized = WhitespaceRegex().Replace(withoutAudioMarkers, " ").Trim();
        normalized = SpaceBeforePunctuationRegex().Replace(normalized, "$1");
        normalized = OrphanPunctuationRegex().Replace(normalized, "").Trim();
        normalized = WaitQuestionLeadInRegex().Replace(normalized, "wait, $1");

        if (normalized.Length == 0)
        {
            return normalized;
        }

        normalized = char.ToUpperInvariant(normalized[0]) + normalized[1..];

        if (!EndsWithSentencePunctuation(normalized))
        {
            normalized += ShouldEndAsQuestion(normalized) ? "?" : ".";
        }
        else if (normalized.EndsWith('.') && ShouldEndAsQuestion(normalized))
        {
            normalized = normalized[..^1] + "?";
        }

        return normalized;
    }

    private static bool EndsWithSentencePunctuation(string text)
    {
        var last = text[^1];
        return last is '.' or '?' or '!';
    }

    private static bool ShouldEndAsQuestion(string text)
    {
        var candidate = text.Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        candidate = candidate.TrimEnd('.', '!', '?').Trim();
        if (candidate.StartsWith("Wait, ", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate["Wait, ".Length..].TrimStart();
        }

        foreach (var starter in QuestionStarters)
        {
            if (candidate.StartsWith(starter + " ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\[[0-9:.]+\s*-->\s*[0-9:.]+\]")]
    private static partial Regex TimestampLineRegex();

    [GeneratedRegex(@"\[\s*(?:(?:BLANK|SILENCE|NO)\s*[_\s-]*AUDIO|MUSIC\s+PLAYING|MUSIC|LAUGHTER|APPLAUSE|NOISE|BACKGROUND\s+NOISE|INAUDIBLE)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex NonSpeechMarkerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+([,.;:!?])")]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex(@"^[,.;:!?]+$")]
    private static partial Regex OrphanPunctuationRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])wait\s*\.\s*(why|what|how)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WaitQuestionLeadInRegex();
}
