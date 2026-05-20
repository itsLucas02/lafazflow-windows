namespace LafazFlow.Windows.Services;

public sealed record LatencyDiagnosticRow(
    DateTimeOffset Timestamp,
    string Id,
    string Status,
    string Model,
    string Threads,
    string Target,
    string ToggleDispatchMs,
    string HotkeyToVisibleMs,
    string RecordingMs,
    string StopHotkeyToQueueMs,
    string QueueWaitMs,
    string PreviewStartMs,
    string PreviewStopMs,
    string WhisperMs,
    string PasteMs,
    string UiHideMs,
    string TotalStopToDoneMs,
    string TotalRecordToDoneMs,
    string Error);
