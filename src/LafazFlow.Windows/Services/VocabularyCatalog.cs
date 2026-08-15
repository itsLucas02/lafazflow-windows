namespace LafazFlow.Windows.Services;

public sealed record VocabularyEntry(string Term, IReadOnlyList<string> HeardVariants);

/// <summary>
/// Single source of truth for the built-in vocabulary: the terms shown in the
/// app's Vocabulary screen, the terms injected into the local Whisper prompt,
/// and the deterministic heard-phrase corrections that normalize ASR drift.
/// </summary>
public static class VocabularyCatalog
{
    public static IReadOnlyList<VocabularyEntry> DefaultEntries { get; } =
    [
        new("Supabase", ["super b's", "superbase", "superbiz", "supabaes", "supabeas", "supabease"]),
        new("Contabo", ["inventabo", "contabo"]),
        new("Vercel", ["vircell"]),
        new("Tailscale", ["tail, skill", "tail skill"]),
        new("Netlify", ["netlify"]),
        new("Mintlify", ["mintlify"]),
        new("Context7", ["contact 7", "contacts 7", "contact seven", "contacts seven"]),
        new("MCP", ["m c p", "em c p"]),
        new("Vite", ["vite", "vite js"]),
        new("MediBrave", ["maddy breath", "medibrief", "mad brave", "medi brave", "maddy brave"]),
        new("Luqman", ["lukamine", "lukman", "luqmen", "l-u-q-m-a-n", "s-n-l-u-q-m-e-n"]),
        new(
            "shadcn",
            [
                "chat cn", "chatcn", "shad cn", "shad c n", "chet's the end", "shut cn",
                "sh*t's the end", "shit, cn", "shut the end", "sh*t-c-n", "shut-see-in",
                "shat-c-n", "shetxian"
            ]),
        new("components.json", ["components dot json"]),
        new("Radix UI", ["radix ui"]),
        new("Tailwind CSS", ["tailwind css"]),
        new("FieldGroup", ["field group"]),
        new("InputGroup", ["input group"]),
        new("Sentry", ["sentry"]),
        new("GitHub", ["github"]),
        new(
            "DeepSeek",
            [
                "deep seek", "deepseek", "deep seq", "deepseq", "deep sec", "deepsec",
                "deepsick", "deep sick"
            ]),
        new("PowerShell", ["power shell", "powershell"]),
        new("Cursor", ["cursor"]),
        new("Stripe", []),
        new("LafazFlow", []),
        new("roadmap", []),
        new("roadmaps", [])
    ];

    public static IReadOnlyList<string> DefaultTerms => DefaultEntries
        .Select(entry => entry.Term)
        .ToArray();
}
