namespace LafazFlow.Windows.Services;

public sealed record ClipboardPasteResult(
    bool RestoreScheduled,
    int RestoreDelayMs,
    PasteKeyGesture Gesture,
    string TargetProcessName);
