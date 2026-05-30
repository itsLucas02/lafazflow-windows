namespace LafazFlow.Windows.Core;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 15;
    public const int DefaultClipboardRestoreDelayMs = 1500;
    public const string DefaultWhisperInitialPrompt =
        "Supabase, Contabo, Vercel, Tailscale, Netlify, Mintlify, Stripe, Context7, MCP, Vite, GitHub, PowerShell, Cursor, LafazFlow, Luqman, MediBrave, "
        + "shadcn, shadcn/ui, shadcn-ui, components.json, Radix UI, Tailwind CSS, FieldGroup, InputGroup, "
        + "npx shadcn@latest, build-web-apps:shadcn, testing, Testing, testing, one, two, three, Testing one two three over, "
        + "wrapper, wrappers, component wrapper, without wrappers, theirs, theirs originally, compare theirs, "
        + "stale, stale document, stale docs, stale file.";

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
    public string CustomVocabularyTerms { get; init; } = "";
    public string CustomCorrectionRules { get; init; } = "";
    public bool EnableVocabularyCorrections { get; init; } = true;
    public bool EnableSoundCues { get; init; } = true;
    public double SoundCueVolume { get; init; } = 0.5;
    public double SoundCueRecordingStartedVolume { get; init; } = 1.0;
    public double SoundCueTranscribingStartedVolume { get; init; } = 1.0;
    public double SoundCueCompletedVolume { get; init; } = 1.45;
    public double SoundCueErrorVolume { get; init; } = 1.0;
    public bool KeepRecordingsForDiagnostics { get; init; }

    public static AppSettings Default { get; } = new();
}
