namespace LafazFlow.Windows.Services;

public static class TextContinuationFormatter
{
    private static readonly HashSet<string> ProtectedLeadingTokens = new(StringComparer.Ordinal)
    {
        "Context7",
        "Luqman",
        "MediBrave",
        "Supabase"
    };

    public static string ApplyTargetContext(string transcript, string textBeforeCaret)
    {
        if (string.IsNullOrWhiteSpace(transcript) || string.IsNullOrWhiteSpace(textBeforeCaret))
        {
            return transcript;
        }

        var context = textBeforeCaret.TrimEnd();
        if (context.Length == 0 || !ShouldContinueMidSentence(context[^1]))
        {
            return transcript;
        }

        var firstLetterIndex = IndexOfFirstLetter(transcript);
        if (firstLetterIndex < 0 || ShouldPreserveLeadingToken(transcript, firstLetterIndex))
        {
            return transcript;
        }

        var first = transcript[firstLetterIndex];
        if (!char.IsUpper(first))
        {
            return transcript;
        }

        return transcript[..firstLetterIndex]
            + char.ToLowerInvariant(first)
            + transcript[(firstLetterIndex + 1)..];
    }

    private static bool ShouldContinueMidSentence(char previousChar)
    {
        return previousChar is ',' or ';' or ':' or '(' or '[' or '{' or '-' or '/';
    }

    private static int IndexOfFirstLetter(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsLetter(text[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ShouldPreserveLeadingToken(string transcript, int firstLetterIndex)
    {
        var tokenEnd = firstLetterIndex;
        while (tokenEnd < transcript.Length && char.IsLetterOrDigit(transcript[tokenEnd]))
        {
            tokenEnd++;
        }

        var token = transcript[firstLetterIndex..tokenEnd];
        if (token == "I")
        {
            return true;
        }

        return ProtectedLeadingTokens.Contains(token)
            || (token.Length > 1 && token.All(char.IsUpper));
    }
}
