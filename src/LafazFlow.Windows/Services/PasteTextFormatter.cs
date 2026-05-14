namespace LafazFlow.Windows.Services;

public static class PasteTextFormatter
{
    public static string EnsureTrailingSeparator(string text)
    {
        if (string.IsNullOrEmpty(text) || char.IsWhiteSpace(text[^1]))
        {
            return text;
        }

        return text + " ";
    }
}
