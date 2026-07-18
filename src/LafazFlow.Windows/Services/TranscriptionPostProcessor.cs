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

        text = ApplyStage(stages, "developer_literal_formatting", text, DeveloperLiteralFormatter.Apply);

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

internal static partial class DeveloperLiteralFormatter
{
    private static readonly HashSet<string> SlashCommandWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "help",
        "init",
        "login",
        "logout",
        "new",
        "settings",
        "start",
        "status",
        "stop",
        "version"
    };

    private static readonly HashSet<string> AtSignDeniedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "class",
        "language",
        "school"
    };

    public static string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var formatted = BacktickPairRegex().Replace(text, match =>
            $"`{TrimLiteralContent(match.Groups["content"].Value)}`");
        formatted = QuotePairRegex().Replace(formatted, match =>
            $"\"{TrimLiteralContent(match.Groups["content"].Value)}\"");
        formatted = PairedDelimiterRegex().Replace(formatted, ReplacePairedDelimiter);
        formatted = DotEnvRegex().Replace(formatted, ".env");
        formatted = FileNameDotExtensionRegex().Replace(formatted, match =>
            $"{match.Groups["name"].Value.ToLowerInvariant()}.{match.Groups["extension"].Value.ToLowerInvariant()}");
        formatted = SlashCommandRegex().Replace(formatted, ReplaceSlashCommand);
        formatted = AtSignRegex().Replace(formatted, ReplaceAtSign);

        return formatted;
    }

    private static string ReplaceSlashCommand(Match match)
    {
        var command = match.Groups["command"].Value;
        return SlashCommandWords.Contains(command)
            ? $"/{command.ToLowerInvariant()}"
            : match.Value;
    }

    private static string ReplaceAtSign(Match match)
    {
        var handle = match.Groups["handle"].Value;
        return AtSignDeniedWords.Contains(handle)
            ? match.Value
            : $"@{handle.ToLowerInvariant()}";
    }

    private static string ReplacePairedDelimiter(Match match)
    {
        var open = match.Groups["open"].Value.ToLowerInvariant();
        var content = TrimLiteralContent(match.Groups["content"].Value);

        return open switch
        {
            "paren" or "parenthesis" => $"({content})",
            "bracket" or "square bracket" => $"[{content}]",
            "brace" or "curly brace" => $"{{{content}}}",
            _ => match.Value
        };
    }

    private static string TrimLiteralContent(string content)
    {
        return content.Trim().Trim(',', '.', ';', ':', '!', '?');
    }

    [GeneratedRegex(@"\bbacktick\s+(?<content>.+?)\s+backtick\b", RegexOptions.IgnoreCase)]
    private static partial Regex BacktickPairRegex();

    [GeneratedRegex(@"\bquote\s+(?<content>.+?)\s+quote\b", RegexOptions.IgnoreCase)]
    private static partial Regex QuotePairRegex();

    [GeneratedRegex(@"\bopen\s+(?<open>paren|parenthesis|bracket|square bracket|brace|curly brace)\s+(?<content>.+?)\s+close\s+\k<open>\b", RegexOptions.IgnoreCase)]
    private static partial Regex PairedDelimiterRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}.])dot\s+env(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex DotEnvRegex();

    [GeneratedRegex(@"\b(?<name>[a-z][a-z0-9-]{1,63})\s+dot\s+(?<extension>json|env|ts|tsx|js|jsx|cs|rs|py|md|yml|yaml|toml|lock|config)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameDotExtensionRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}/])(?:forward\s+slash|slash)\s+(?<command>[a-z][a-z0-9-]{1,63})(?![\p{L}\p{N}-])", RegexOptions.IgnoreCase)]
    private static partial Regex SlashCommandRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}@])at\s+sign\s+(?<handle>[a-z][a-z0-9_]{1,31})(?![\p{L}\p{N}_-])", RegexOptions.IgnoreCase)]
    private static partial Regex AtSignRegex();
}

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
