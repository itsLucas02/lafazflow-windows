namespace LafazFlow.Windows.Services;

public sealed record HotkeyDiagnosticEvent(
    DateTimeOffset Timestamp,
    string Event,
    string Gesture,
    string Accepted,
    string State,
    string DispatchMs,
    string Reason,
    string Target);
