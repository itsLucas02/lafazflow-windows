namespace LafazFlow.Windows.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int DefaultClipboardRestoreDelayMs = 1500;
    public const string DefaultWhisperInitialPrompt =
        "Supabase, Vercel, Tailscale, Netlify, Mintlify, GitHub, PowerShell, Cursor, LafazFlow.";

    public int SettingsSchemaVersion { get; init; } = CurrentSchemaVersion;
    public string HotkeyGesture { get; init; } = "DoubleShift";
    public HotkeyMode HotkeyMode { get; init; } = HotkeyMode.Hybrid;
    public string WhisperCliPath { get; init; } = "";
    public string ModelPath { get; init; } = "";
    public int WhisperThreads { get; init; } = 16;
    public bool RestoreClipboardAfterPaste { get; init; } = true;
    public int ClipboardRestoreDelayMs { get; init; } = DefaultClipboardRestoreDelayMs;
    public bool AppendTrailingSpace { get; init; } = true;
    public string WhisperInitialPrompt { get; init; } = DefaultWhisperInitialPrompt;
    public bool EnableVocabularyCorrections { get; init; } = true;
    public bool KeepRecordingsForDiagnostics { get; init; }

    public static AppSettings Default { get; } = new();
}
