namespace LafazFlow.Windows.Core;

public sealed record AppSettings
{
    public string HotkeyGesture { get; init; } = "DoubleShift";
    public HotkeyMode HotkeyMode { get; init; } = HotkeyMode.Hybrid;
    public string WhisperCliPath { get; init; } = "";
    public string ModelPath { get; init; } = "";
    public bool RestoreClipboardAfterPaste { get; init; } = true;
    public int ClipboardRestoreDelayMs { get; init; } = 250;
    public bool AppendTrailingSpace { get; init; }
    public bool KeepRecordingsForDiagnostics { get; init; }

    public static AppSettings Default { get; } = new();
}
