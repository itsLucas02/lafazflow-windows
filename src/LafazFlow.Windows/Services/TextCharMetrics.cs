namespace LafazFlow.Windows.Services;

public static class TextCharMetrics
{
    public static int CharacterCount(string text)
    {
        return text?.Length ?? 0;
    }

    public static string FinalCharCategory(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "none";
        }

        var final = text[^1];
        if (char.IsLetter(final))
        {
            return "letter";
        }

        if (char.IsDigit(final))
        {
            return "digit";
        }

        if (char.IsPunctuation(final) || char.IsSymbol(final))
        {
            return "punct";
        }

        if (char.IsWhiteSpace(final))
        {
            return "space";
        }

        return "other";
    }
}
