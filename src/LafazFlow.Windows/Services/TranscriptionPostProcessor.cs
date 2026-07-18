using System.Text.RegularExpressions;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class TranscriptionPostProcessor
{
    public TranscriptionPostProcessingResult Process(TranscriptionPostProcessingRequest request)
    {
        var stages = new List<TranscriptionPostProcessingStageResult>();
        var text = ApplyStage(stages, "raw_cleanup", request.RawTranscript, RawTranscriptionCleanup.Apply);

        if (request.Settings.EnableVocabularyCorrections)
        {
            text = ApplyStage(stages, "vocabulary", text, value =>
                VocabularyCorrectionService.Apply(value, request.Settings.CustomCorrectionRules));
        }
        else
        {
            stages.Add(new TranscriptionPostProcessingStageResult("vocabulary", Changed: false, Skipped: true));
        }

        text = ApplyStage(stages, "target_context", text, value =>
            TextContinuationFormatter.ApplyTargetContext(value, request.TargetTextBeforeCaret));

        if (request.Settings.AppendTrailingSpace)
        {
            text = ApplyStage(stages, "trailing_separator", text, PasteTextFormatter.EnsureTrailingSeparator);
        }
        else
        {
            stages.Add(new TranscriptionPostProcessingStageResult("trailing_separator", Changed: false, Skipped: true));
        }

        return new TranscriptionPostProcessingResult(text, stages);
    }

    private static string ApplyStage(
        List<TranscriptionPostProcessingStageResult> stages,
        string name,
        string input,
        Func<string, string> apply)
    {
        var output = apply(input);
        stages.Add(new TranscriptionPostProcessingStageResult(name, output != input, Skipped: false));
        return output;
    }
}

public sealed record TranscriptionPostProcessingRequest(
    string RawTranscript,
    AppSettings Settings,
    string TargetTextBeforeCaret);

public sealed record TranscriptionPostProcessingResult(
    string Text,
    IReadOnlyList<TranscriptionPostProcessingStageResult> Stages);

public sealed record TranscriptionPostProcessingStageResult(
    string Stage,
    bool Changed,
    bool Skipped);

internal static partial class RawTranscriptionCleanup
{
    public static string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        var hadLeadingFiller = LeadingFillerWordRegex().IsMatch(text);
        var cleaned = FillerWordRegex().Replace(text, " ");
        cleaned = CollapseShortWordStutters(cleaned);
        cleaned = SpaceBeforePunctuationRegex().Replace(cleaned, "$1");
        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();
        if (hadLeadingFiller)
        {
            cleaned = CapitalizeFirstLetter(cleaned);
        }

        return cleaned;
    }

    private static string CapitalizeFirstLetter(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsLetter(text[index]))
            {
                continue;
            }

            if (char.IsUpper(text[index]))
            {
                return text;
            }

            return text[..index] + char.ToUpperInvariant(text[index]) + text[(index + 1)..];
        }

        return text;
    }

    private static string CollapseShortWordStutters(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "";
        }

        var result = new List<string>(words.Length);
        var index = 0;
        while (index < words.Length)
        {
            var current = words[index];
            var currentCore = WordCore(current);
            if (!IsShortAlphabeticToken(currentCore))
            {
                result.Add(current);
                index++;
                continue;
            }

            var count = 1;
            while (index + count < words.Length
                && string.Equals(
                    WordCore(words[index + count]),
                    currentCore,
                    StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }

            result.Add(current);
            index += count >= 3 ? count : 1;
        }

        return string.Join(' ', result);
    }

    private static string WordCore(string word)
    {
        return word.Trim(',', '.', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}');
    }

    private static bool IsShortAlphabeticToken(string word)
    {
        return word.Length is > 0 and <= 2 && word.All(char.IsLetter);
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:uh+|um+|uhm+|umm+|hmm+|hm+|mmm+)(?:\s*[,.;:!?])?(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex FillerWordRegex();

    [GeneratedRegex(@"^\s*(?:uh+|um+|uhm+|umm+|hmm+|hm+|mmm+)(?:\s*[,.;:!?])?(?=\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingFillerWordRegex();

    [GeneratedRegex(@"\s+([,.;:!?])")]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
