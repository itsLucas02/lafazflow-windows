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
        var customTerms = NormalizeCustomTerms(customVocabularyTerms);
        if (customTerms.Count == 0)
        {
            return builtInPrompt.Trim();
        }

        var basePrompt = builtInPrompt.Trim();
        var separator = basePrompt.EndsWith('.') ? " " : ". ";
        return $"{basePrompt}{separator}Custom vocabulary: {string.Join(", ", customTerms)}.";
    }

    private static IReadOnlyList<string> NormalizeCustomTerms(string customVocabularyTerms)
    {
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in customVocabularyTerms.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            var term = rawLine.Trim();
            if (term.Length == 0 || !seen.Add(term))
            {
                continue;
            }

            terms.Add(term);
        }

        return terms;
    }
}
