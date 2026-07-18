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

    private static readonly string[] QuestionLeadIns =
    [
        "so",
        "but"
    ];

    public static string Format(string text)
    {
        var withoutTimestamps = TimestampLineRegex().Replace(text, " ");
        var withoutAudioMarkers = NonSpeechMarkerRegex().Replace(withoutTimestamps, " ");
        var normalized = WhitespaceRegex().Replace(withoutAudioMarkers, " ").Trim();
        normalized = SpaceBeforePunctuationRegex().Replace(normalized, "$1");
        normalized = OrphanPunctuationRegex().Replace(normalized, "").Trim();
        normalized = WaitQuestionLeadInRegex().Replace(normalized, "wait, $1");
        normalized = AndContinuationBreakRegex().Replace(normalized, ", and ");
        normalized = SoThatContinuationBreakRegex().Replace(normalized, ", so that");
        normalized = RepairCommandReminderQuestions(normalized);
        normalized = RepairDeclarativeQuestionMarks(normalized);

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
        else if (normalized.EndsWith('?') && !ShouldKeepQuestionMark(normalized))
        {
            normalized = normalized[..^1] + ".";
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
        if (StartsWithCommandReminder(candidate))
        {
            return false;
        }

        if (candidate.StartsWith("Wait, ", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate["Wait, ".Length..].TrimStart();
        }

        foreach (var leadIn in QuestionLeadIns)
        {
            if (candidate.StartsWith(leadIn + " ", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[(leadIn.Length + 1)..].TrimStart();
                break;
            }
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

    private static string RepairCommandReminderQuestions(string text)
    {
        return QuestionSentenceSegmentRegex().Replace(text, match =>
        {
            var sentence = match.Groups["sentence"].Value;
            if (!StartsWithCommandReminder(sentence))
            {
                return match.Value;
            }

            return match.Groups["prefix"].Value + sentence.TrimEnd('?') + ".";
        });
    }

    private static string RepairDeclarativeQuestionMarks(string text)
    {
        return QuestionSentenceSegmentRegex().Replace(text, match =>
        {
            var sentence = match.Groups["sentence"].Value;
            if (ShouldKeepQuestionMark(sentence))
            {
                return match.Value;
            }

            return match.Groups["prefix"].Value + sentence.TrimEnd('?') + ".";
        });
    }

    private static bool ShouldKeepQuestionMark(string sentence)
    {
        var candidate = sentence.Trim();
        if (ShouldEndAsQuestion(candidate))
        {
            return true;
        }

        candidate = candidate.TrimEnd('?').Trim();
        if (IsShortTagQuestion(candidate))
        {
            return true;
        }

        return !LooksDeclarative(candidate);
    }

    private static bool IsShortTagQuestion(string sentence)
    {
        var normalized = sentence.ToLowerInvariant();
        return normalized is "right" or "correct" or "okay" or "ok" or "yeah" or "yes" or "no"
            || normalized is "isn't it" or "is it" or "does it" or "you know";
    }

    private static bool LooksDeclarative(string sentence)
    {
        var trimmed = sentence.TrimStart();
        return DeclarativeLeadInRegex().IsMatch(trimmed)
            || DeclarativeSubjectRegex().IsMatch(trimmed)
            || DeclarativeFragmentRegex().IsMatch(trimmed);
    }

    private static bool StartsWithCommandReminder(string sentence)
    {
        var trimmed = sentence.TrimStart();
        return trimmed.StartsWith("Also don't forget to ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Don't forget to ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Do not forget to ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Please remember", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Remember to ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Make sure to ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Please make sure to ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Ensure that ", StringComparison.OrdinalIgnoreCase);
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

    [GeneratedRegex(@"\.\s+And\s+(?=(?:then|there|once|maybe|therefore|it|they|we|you)\b)")]
    private static partial Regex AndContinuationBreakRegex();

    [GeneratedRegex(@"[.?]\s+So\s+that\b", RegexOptions.IgnoreCase)]
    private static partial Regex SoThatContinuationBreakRegex();

    [GeneratedRegex(@"(?<prefix>^|(?<=[.!?])\s+)(?<sentence>[^.!?]+\?)")]
    private static partial Regex QuestionSentenceSegmentRegex();

    [GeneratedRegex(@"^(?:currently|basically|actually|honestly|technically|literally|probably|maybe|perhaps|for your information|of course|by the way|not only)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DeclarativeLeadInRegex();

    [GeneratedRegex(@"^(?:i|i'm|i am|we|we're|we are|this|that|it|it's|it is|there|there's|there is|the|my|your|our|all of this|everything)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DeclarativeSubjectRegex();

    [GeneratedRegex(@"^(?:of course|basically|for sure|definitely|probably|maybe|perhaps)$", RegexOptions.IgnoreCase)]
    private static partial Regex DeclarativeFragmentRegex();
}
