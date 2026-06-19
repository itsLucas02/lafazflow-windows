namespace LafazFlow.Windows.Services;

public sealed record HotkeyDiagnosticWrite(
    string Event,
    string Gesture = "DoubleShift",
    string Accepted = "na",
    string State = "na",
    string DispatchMs = "na",
    string Reason = "na",
    string Target = "na");

public interface IHotkeyDiagnostics
{
    void Log(HotkeyDiagnosticWrite entry);
}
