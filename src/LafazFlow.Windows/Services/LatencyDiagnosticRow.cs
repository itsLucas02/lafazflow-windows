namespace LafazFlow.Windows.Services;

public sealed record LatencyDiagnosticRow(
    DateTimeOffset Timestamp,
    string Id,
    string Status,
    string Model,
    string Threads,
    string Target,
    string RecordingMs,
    string QueueWaitMs,
    string WhisperMs,
    string PasteMs,
    string TotalStopToDoneMs,
    string TotalRecordToDoneMs,
    string Error);
