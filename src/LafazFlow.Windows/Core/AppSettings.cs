namespace LafazFlow.Windows.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 3;
    public const int DefaultClipboardRestoreDelayMs = 1500;
    public const string DefaultWhisperInitialPrompt =
        "Supabase, Vercel, Tailscale, Netlify, Mintlify, GitHub, PowerShell, Cursor, LafazFlow, Luqman, "
        + "shadcn, shadcn/ui, shadcn-ui, components.json, Radix UI, Tailwind CSS, FieldGroup, InputGroup, "
        + "npx shadcn@latest, build-web-apps:shadcn.";

    public int SettingsSchemaVersion { get; init; } = CurrentSchemaVersion;
    public string HotkeyGesture { get; init; } = "DoubleShift";
    public HotkeyMode HotkeyMode { get; init; } = HotkeyMode.Hybrid;
    public string WhisperCliPath { get; init; } = "";
    public string CudaWhisperCliPath { get; init; } = @"C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe";
    public string ModelPath { get; init; } = "";
    public string QualityModelPath { get; init; } = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin";
    public int WhisperThreads { get; init; } = 16;
    public TranscriptionProfile TranscriptionProfile { get; init; } = TranscriptionProfile.Fast;
    public WhisperBackend WhisperBackend { get; init; } = WhisperBackend.Cpu;
    public bool EnableVad { get; init; }
    public string VadModelPath { get; init; } = @"C:\Models\whisper\ggml-silero-v5.1.2.bin";
    public bool RestoreClipboardAfterPaste { get; init; } = true;
    public int ClipboardRestoreDelayMs { get; init; } = DefaultClipboardRestoreDelayMs;
    public bool AppendTrailingSpace { get; init; } = true;
    public bool ShowLiveTranscriptPreview { get; init; } = true;
    public string WhisperInitialPrompt { get; init; } = DefaultWhisperInitialPrompt;
    public bool EnableVocabularyCorrections { get; init; } = true;
    public bool KeepRecordingsForDiagnostics { get; init; }

    public static AppSettings Default { get; } = new();
}
