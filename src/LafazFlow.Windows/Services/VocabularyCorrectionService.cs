using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public static partial class VocabularyCorrectionService
{
    private static readonly (Regex Pattern, string Replacement)[] DefaultCorrections = BuildDefaultCorrections();

    private static (Regex Pattern, string Replacement)[] BuildDefaultCorrections()
    {
        var corrections = new List<(Regex Pattern, string Replacement)>();
        foreach (var entry in VocabularyCatalog.DefaultEntries)
        {
            foreach (var variant in entry.HeardVariants)
            {
                corrections.Add((PhraseRegex(variant), entry.Term));
            }
        }

        // Linguistic/typo corrections that are not vocabulary words and therefore
        // stay out of the visible Vocabulary screen.
        corrections.AddRange(
        [
            (PhraseRegex("repeteness"), "rapidness"),
            (PhraseRegex("comit"), "commit"),
            (PhraseRegex("git come in"), "git commit"),
            (PhraseRegex("git comes in"), "git commit"),
            (PhraseRegex("come in and push"), "commit and push"),
            (PhraseRegex("comes in and push"), "commit and push")
        ]);

        return corrections.ToArray();
    }

    public static string ApplyDefaults(string text)
    {
        var corrected = text;
        foreach (var (pattern, replacement) in DefaultCorrections)
        {
            corrected = pattern.Replace(corrected, replacement);
        }

        corrected = FixRoadmapTerminology(corrected);
        corrected = FixDeepSeekPhoneticFamily(corrected);
        corrected = FixTestingDictationThats(corrected);
        corrected = FixTestingDictationLetsThink(corrected);
        corrected = FixDeveloperDictationPhrases(corrected);
        corrected = FixSpelledLetterDictation(corrected);
        corrected = FixConversationalWeightAsWait(corrected);
        corrected = FixConsentFormCompound(corrected);
        corrected = FixEnglishDokumenDrift(corrected);
        corrected = FixWrapperDictationInCodingContext(corrected);
        corrected = FixTheirsDictationDrsInUiComparisonContext(corrected);
        corrected = FixStaleDocumentDictationContext(corrected);
        corrected = FixStripeDictationInPaymentContext(corrected);
        corrected = FixBestBangForBuckDictationContext(corrected);
        corrected = FixBetterStackErrorsDictationContext(corrected);
        corrected = FixDeveloperEdgeCasesDictationContext(corrected);
        corrected = NormalizeProtectedDeveloperTokens(corrected);

        return corrected;
    }

    private static string FixDeepSeekPhoneticFamily(string text)
    {
        var corrected = DeepSeekFamilyRegex().Replace(text, "DeepSeek");
        corrected = DeepSeekDipFamilyRegex().Replace(corrected, "DeepSeek");
        return corrected;
    }

    private static string FixRoadmapTerminology(string text)
    {
        var corrected = RoadMapRegex().Replace(text, match => RoadmapTerm(match.Value, plural: false));
        corrected = RoadMapsRegex().Replace(corrected, match => RoadmapTerm(match.Value, plural: true));
        corrected = RouteMapPlanningContextRegex().Replace(
            corrected,
            match => match.Groups[1].Value + RoadmapTerm(match.Groups[2].Value, plural: match.Groups[2].Value.EndsWith("s", StringComparison.OrdinalIgnoreCase)));
        corrected = RouteMapHandoffContextRegex().Replace(
            corrected,
            match => match.Groups[1].Value + RoadmapTerm(match.Groups[2].Value, plural: match.Groups[2].Value.EndsWith("s", StringComparison.OrdinalIgnoreCase)));
        return corrected;
    }

    private static string RoadmapTerm(string matched, bool plural)
    {
        var replacement = plural ? "roadmaps" : "roadmap";
        return matched.Length > 0 && char.IsUpper(matched[0])
            ? char.ToUpperInvariant(replacement[0]) + replacement[1..]
            : replacement;
    }

    public static string Apply(string text, string customCorrectionRules)
    {
        return ApplyCustomRules(ApplyDefaults(text), customCorrectionRules);
    }

    public static IReadOnlyList<string> ValidateCustomCorrectionRules(string customCorrectionRules)
    {
        var errors = new List<string>();
        _ = ParseCustomCorrectionRules(customCorrectionRules, errors);

        return errors;
    }

    private static string ApplyCustomRules(string text, string customCorrectionRules)
    {
        var corrected = text;
        foreach (var rule in ParseCustomCorrectionRules(customCorrectionRules, errors: null))
        {
            corrected = PhraseRegex(rule.Heard).Replace(corrected, rule.Replacement);
        }

        return corrected;
    }

    private static IReadOnlyList<CustomCorrectionRule> ParseCustomCorrectionRules(
        string customCorrectionRules,
        List<string>? errors)
    {
        if (string.IsNullOrWhiteSpace(customCorrectionRules))
        {
            return [];
        }

        var rules = new List<CustomCorrectionRule>();
        var lines = customCorrectionRules.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var arrowIndex = line.IndexOf("=>", StringComparison.Ordinal);
            if (arrowIndex < 0)
            {
                errors?.Add($"Correction rule line {index + 1} must use 'heard phrase => corrected phrase'.");
                continue;
            }

            var heard = line[..arrowIndex].Trim();
            var replacement = line[(arrowIndex + 2)..].Trim();
            if (heard.Length == 0 || replacement.Length == 0)
            {
                errors?.Add($"Correction rule line {index + 1} must include text before and after '=>'.");
                continue;
            }

            rules.Add(new CustomCorrectionRule(heard, replacement));
        }

        return rules;
    }

    private static Regex PhraseRegex(string phrase)
    {
        return new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);
    }

    private sealed record CustomCorrectionRule(string Heard, string Replacement);

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

    private static string FixTestingDictationLetsThink(string text)
    {
        return TestingLeadLetsThinkRegex().Replace(text, match =>
        {
            var count = match.Groups[1].Value.Contains('1')
                ? "1, 2, 3"
                : match.Groups[1].Value.Contains(',')
                    ? "one, two, three"
                    : "one two three";
            var over = match.Groups[2].Success ? ", over" : "";
            return char.IsUpper(match.Value[0])
                ? $"Testing {count}{over}"
                : $"testing {count}{over}";
        });
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])let['â€™]?s\s+think\s+((?:one\s*,?\s*two\s*,?\s*three)|(?:1\s*,?\s*2\s*,?\s*3))(\s*,?\s*over)?", RegexOptions.IgnoreCase)]
    private static partial Regex TestingLeadLetsThinkRegex();

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

    private static string FixConsentFormCompound(string text)
    {
        return ConsentFormCompoundRegex().Replace(text, match =>
        {
            return char.IsUpper(match.Value[0]) ? "Consent form" : "consent form";
        });
    }

    private static string FixEnglishDokumenDrift(string text)
    {
        return EnglishDokumenDriftRegex().Replace(text, match =>
        {
            return char.IsUpper(match.Value[0]) ? "Document" : "document";
        });
    }

    private static string FixWrapperDictationInCodingContext(string text)
    {
        return WrapperCodingContextRegex().Replace(text, match => $"{match.Groups[1].Value}wrappers");
    }

    private static string FixTheirsDictationDrsInUiComparisonContext(string text)
    {
        return TheirsDrsContextRegex().Replace(text, match => $"{match.Groups[1].Value}theirs");
    }

    private static string FixStaleDocumentDictationContext(string text)
    {
        return StaleDocumentContextRegex().Replace(text, match => $"stale {match.Groups[1].Value.ToLowerInvariant()}");
    }

    private static string FixStripeDictationInPaymentContext(string text)
    {
        var corrected = StripeActionContextRegex().Replace(text, match => $"{match.Groups[1].Value}Stripe");
        return StripeProductContextRegex().Replace(corrected, match => $"Stripe {match.Groups[1].Value}");
    }

    private static string FixBestBangForBuckDictationContext(string text)
    {
        return BestBangForBuckContextRegex().Replace(text, match =>
            $"{match.Groups[1].Value}best bang for buck{match.Groups[2].Value}");
    }

    private static string FixBetterStackErrorsDictationContext(string text)
    {
        return BetterStackErrorsContextRegex().Replace(text, "Better Stack Errors");
    }

    private static string FixDeveloperEdgeCasesDictationContext(string text)
    {
        return AgeCasesContextRegex().Replace(text, match =>
            $"{match.Groups[1].Value}edge cases{match.Groups[2].Value}");
    }

    private static string FixSpelledLetterDictation(string text)
    {
        var corrected = StaffSpelledWithHyphensRegex().Replace(text, "staff");
        corrected = StaffSpelledWithSpacesRegex().Replace(corrected, "staff");
        corrected = CapitalTRegex().Replace(corrected, "T");
        corrected = LetterTRegex().Replace(corrected, "T");

        return corrected;
    }

    private static string FixConversationalWeightAsWait(string text)
    {
        var corrected = WeightQuestionLeadInRegex().Replace(text, match =>
        {
            var questionWord = match.Groups[1].Value.ToLowerInvariant();
            var wait = char.IsUpper(match.Value[0]) ? "Wait" : "wait";
            return $"{wait}, {questionWord}";
        });
        corrected = WaitQuestionPeriodRegex().Replace(corrected, "$1?");

        return WeightAMinuteRegex().Replace(corrected, match =>
            char.IsUpper(match.Value[0]) ? "Wait a minute" : "wait a minute");
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

    [GeneratedRegex(@"(?<![\p{L}\p{N}])s\s*-\s*t\s*-\s*a\s*-\s*f\s*-\s*f(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex StaffSpelledWithHyphensRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])s\s+t\s+a\s+f\s+f(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex StaffSpelledWithSpacesRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])capital\s+t(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex CapitalTRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])letter\s+t(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex LetterTRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])consen(?:t)?\s*form(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex ConsentFormCompoundRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])dokumen(?=\s+(?:everything|this|that|it)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishDokumenDriftRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])((?:without\s+any|with\s+no|no|with|component)\s+)rappers(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex WrapperCodingContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])((?:see|compare|use|took)\s+)DRs(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex TheirsDrsContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:still|steel)\s+(document|docs|file)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex StaleDocumentContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])((?:use|using|open|install|configure|setup|set\s+up|add|integrate|enable|connect|call|check)\s+)(?:strike|stripe)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex StripeActionContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:strike|stripe)\s+(checkout|billing|payment|payments|webhook|webhooks|api|sdk|dashboard|integration|customer|customers|subscription|subscriptions)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex StripeProductContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(\b(?:a\s+|the\s+)?)(?:best\s+)?(?:bank|bang)\s+for\s+(?:bug|buck|bulk)(\s+(?:option|choice|tool|tools|service|services|stack|stacks|platform|platforms|between|for)\b)?", RegexOptions.IgnoreCase)]
    private static partial Regex BestBangForBuckContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:batter\s+stack\s+errors|battle\s+stack\s+errors|better\s+stack\s+eros)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex BetterStackErrorsContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(\b(?:terms?\s+of|handling|handle|handles|scenario|scenarios|problem|problems|especially|around|cover|covers|test|tests|testing|regression|regressions)\s+)age\s+cases(\b)", RegexOptions.IgnoreCase)]
    private static partial Regex AgeCasesContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])weight\s+(why|what|how)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WeightQuestionLeadInRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])weight\s+a\s+minute(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex WeightAMinuteRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])road\s+map(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex RoadMapRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(deep[\s-]?(?:seek|seq|sec|sick|stick|six|sea|sik|sique|6))(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex DeepSeekFamilyRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(dip[\s-]?(?:sick|seq|sec|seek|sea|six))(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex DeepSeekDipFamilyRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])road\s+maps(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex RoadMapsRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])((?:project|implementation|milestone|backlog|engineering|software|product|sprint|release|development|feature|delivery|agent|handoff)\s+)(route\s+maps?)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex RouteMapPlanningContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])((?:hand(?:ing|ed)?\s+off|handoff|hand-off|pass(?:ing|ed)?|give|gave|giving)\s+(?:the|this|that)?\s*)(route\s+maps?)(?=\s+(?:to|for)\s+(?:the\s+)?(?:agent|AI|assistant|engineer|developer|team|implementation|engineering|handoff))", RegexOptions.IgnoreCase)]
    private static partial Regex RouteMapHandoffContextRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(wait,\s+(?:why|what|how)\b[^.!?]*)\.", RegexOptions.IgnoreCase)]
    private static partial Regex WaitQuestionPeriodRegex();
}
