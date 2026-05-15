using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public static partial class VocabularyCorrectionService
{
    private static readonly (Regex Pattern, string Replacement)[] DefaultCorrections =
    [
        (PhraseRegex("super b's"), "Supabase"),
        (PhraseRegex("superbase"), "Supabase"),
        (PhraseRegex("vircell"), "Vercel"),
        (PhraseRegex("tail, skill"), "Tailscale"),
        (PhraseRegex("tail skill"), "Tailscale"),
        (PhraseRegex("netlify"), "Netlify"),
        (PhraseRegex("mintlify"), "Mintlify"),
        (PhraseRegex("maddy breath"), "MediBrave"),
        (PhraseRegex("medibrief"), "MediBrave"),
        (PhraseRegex("mad brave"), "MediBrave"),
        (PhraseRegex("medi brave"), "MediBrave"),
        (PhraseRegex("maddy brave"), "MediBrave"),
        (PhraseRegex("github"), "GitHub"),
        (PhraseRegex("power shell"), "PowerShell"),
        (PhraseRegex("powershell"), "PowerShell"),
        (PhraseRegex("cursor"), "Cursor")
    ];

    public static string ApplyDefaults(string text)
    {
        var corrected = text;
        foreach (var (pattern, replacement) in DefaultCorrections)
        {
            corrected = pattern.Replace(corrected, replacement);
        }

        return corrected;
    }

    private static Regex PhraseRegex(string phrase)
    {
        return new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);
    }
}
