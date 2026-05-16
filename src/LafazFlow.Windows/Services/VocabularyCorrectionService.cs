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
        (PhraseRegex("lukamine"), "Luqman"),
        (PhraseRegex("lukman"), "Luqman"),
        (PhraseRegex("luqmen"), "Luqman"),
        (PhraseRegex("l-u-q-m-a-n"), "Luqman"),
        (PhraseRegex("s-n-l-u-q-m-e-n"), "Luqman"),
        (PhraseRegex("repeteness"), "rapidness"),
        (PhraseRegex("comit"), "commit"),
        (PhraseRegex("git come in"), "git commit"),
        (PhraseRegex("git comes in"), "git commit"),
        (PhraseRegex("come in and push"), "commit and push"),
        (PhraseRegex("comes in and push"), "commit and push"),
        (PhraseRegex("chat cn"), "shadcn"),
        (PhraseRegex("chatcn"), "shadcn"),
        (PhraseRegex("shad cn"), "shadcn"),
        (PhraseRegex("shad c n"), "shadcn"),
        (PhraseRegex("chet's the end"), "shadcn"),
        (PhraseRegex("shut cn"), "shadcn"),
        (PhraseRegex("sh*t's the end"), "shadcn"),
        (PhraseRegex("shit, cn"), "shadcn"),
        (PhraseRegex("shut the end"), "shadcn"),
        (PhraseRegex("sh*t-c-n"), "shadcn"),
        (PhraseRegex("shut-see-in"), "shadcn"),
        (PhraseRegex("shat-c-n"), "shadcn"),
        (PhraseRegex("shetxian"), "shadcn"),
        (PhraseRegex("components dot json"), "components.json"),
        (PhraseRegex("radix ui"), "Radix UI"),
        (PhraseRegex("tailwind css"), "Tailwind CSS"),
        (PhraseRegex("field group"), "FieldGroup"),
        (PhraseRegex("input group"), "InputGroup"),
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

        corrected = FixTestingDictationThats(corrected);
        corrected = FixDeveloperDictationPhrases(corrected);
        corrected = NormalizeProtectedDeveloperTokens(corrected);

        return corrected;
    }

    private static Regex PhraseRegex(string phrase)
    {
        return new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);
    }

    private static string FixTestingDictationThats(string text)
    {
        var corrected = RepeatedThatsRegex().Replace(text, match =>
        {
            var replacement = ThatsRegex().Replace(match.Value, "test");
            return char.IsUpper(match.Value[0])
                ? char.ToUpperInvariant(replacement[0]) + replacement[1..]
                : replacement;
        });

        return TestingLeadThatsRegex().Replace(corrected, match =>
            char.IsUpper(match.Value[0]) ? "Test" : "test");
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])that['’]?s(?:\s*,\s*that['’]?s)+(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex RepeatedThatsRegex();

    [GeneratedRegex(@"that['’]?s", RegexOptions.IgnoreCase)]
    private static partial Regex ThatsRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])that['’]?s(?=\s+(?:\d|one\b|two\b|three\b|1-2-3\b))", RegexOptions.IgnoreCase)]
    private static partial Regex TestingLeadThatsRegex();

    private static string FixDeveloperDictationPhrases(string text)
    {
        var corrected = ReuseWhateverWeUseHaveRegex().Replace(text, "reuse whatever we have");
        corrected = InstallOnesReuseForeverRegex().Replace(corrected, "Install once, reuse forever");
        corrected = WhatDoYouThinkQuestionRegex().Replace(corrected, "what do you think.");

        return corrected;
    }

    private static string NormalizeProtectedDeveloperTokens(string text)
    {
        var corrected = ShadcnUiSkillTokenRegex().Replace(text, "$shadcn-ui");
        corrected = BuildWebAppsShadcnSkillTokenRegex().Replace(corrected, "$build-web-apps:shadcn");
        corrected = SpaceBeforeProtectedPunctuationRegex().Replace(corrected, "$1");

        return corrected;
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])reuse\s+whatever\s+we\s+use\s+have(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex ReuseWhateverWeUseHaveRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])install\s+one['â€™]?s\s+reuse\s+forever(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex InstallOnesReuseForeverRegex();

    [GeneratedRegex(@"what\s+do\s+you\s+think\?(?=\s+Everything\s+is\s+documented)", RegexOptions.IgnoreCase)]
    private static partial Regex WhatDoYouThinkQuestionRegex();

    [GeneratedRegex(@"\$\s*shadcn\s*-\s*ui", RegexOptions.IgnoreCase)]
    private static partial Regex ShadcnUiSkillTokenRegex();

    [GeneratedRegex(@"\$\s*build\s*-\s*web\s*-\s*apps\s*:\s*shadcn", RegexOptions.IgnoreCase)]
    private static partial Regex BuildWebAppsShadcnSkillTokenRegex();

    [GeneratedRegex(@"\s+([.:])(?=\s|$)")]
    private static partial Regex SpaceBeforeProtectedPunctuationRegex();
}
