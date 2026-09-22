using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public static class WhisperPromptBuilder
{
    public static string BuildVocabularyPrompt(AppSettings settings)
    {
        return BuildVocabularyPrompt(settings.WhisperInitialPrompt, settings.CustomVocabularyTerms);
    }

    public static string BuildVocabularyPrompt(string builtInPrompt, string customVocabularyTerms)
    {
        var basePrompt = builtInPrompt.Trim();
        var terms = NormalizeTerms(SplitCustomTerms(customVocabularyTerms))
            .Where(term => !basePrompt.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (terms.Length == 0)
        {
            return basePrompt;
        }

        var separator = basePrompt.EndsWith('.') ? " " : ". ";
        return $"{basePrompt}{separator}Custom vocabulary: {string.Join(", ", terms)}.";
    }

    private static IReadOnlyList<string> SplitCustomTerms(string customVocabularyTerms)
    {
        var terms = new List<string>();
        foreach (var rawLine in customVocabularyTerms.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            var term = rawLine.Trim();
            if (term.Length > 0)
            {
                terms.Add(term);
            }
        }

        return terms;
    }

    private static IReadOnlyList<string> NormalizeTerms(IEnumerable<string> rawTerms)
    {
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawTerm in rawTerms)
        {
            var term = rawTerm.Trim();
            if (term.Length == 0 || !seen.Add(term))
            {
                continue;
            }

            terms.Add(term);
        }

        return terms;
    }
}
