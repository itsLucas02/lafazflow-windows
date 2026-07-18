using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class FileHotkeyDiagnosticsTests
{
    [Fact]
    public void LogWritesPrivacySafeHotkeyLine()
    {
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "lafazflow.log");
        var diagnostics = new FileHotkeyDiagnostics(logPath);

        diagnostics.Log(new HotkeyDiagnosticWrite(
            Event: "toggle_start",
            Gesture: "DoubleShift",
            Accepted: "true",
            State: "Recording",
            DispatchMs: "12",
            Reason: "second shift with spaces",
            Target: "Cursor"));

        var line = File.ReadAllText(logPath);
        Assert.Contains("HOTKEY event=toggle_start", line);
        Assert.Contains("gesture=DoubleShift", line);
        Assert.Contains("accepted=true", line);
        Assert.Contains("state=Recording", line);
        Assert.Contains("dispatch_ms=12", line);
        Assert.Contains("reason=second_shift_with_spaces", line);
        Assert.Contains("target=Cursor", line);
        Assert.DoesNotContain(Environment.UserName, line);
    }

    [Fact]
    public void LogSanitizesUnsafeTargetsAndReasons()
    {
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "lafazflow.log");
        var diagnostics = new FileHotkeyDiagnostics(logPath);

        diagnostics.Log(new HotkeyDiagnosticWrite(
            Event: "preview failed",
            Reason: @"C:\Users\User\private path",
            Target: "Window Title With Spaces"));

        var line = File.ReadAllText(logPath);
        Assert.Contains("event=preview_failed", line);
        Assert.Contains("reason=path_redacted", line);
        Assert.Contains("target=Window_Title_With_Spaces", line);
        Assert.DoesNotContain(@"\", line);
        Assert.DoesNotContain("private", line);
    }

    [Fact]
    public void LogTrimsOversizedOldLogBeforeAppending()
    {
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "lafazflow.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var oldLine = $"[{DateTimeOffset.Now.AddDays(-30):O}] HOTKEY event=rejected gesture=DoubleShift accepted=false state=na dispatch_ms=na reason=already_down target=na";
        File.WriteAllLines(logPath, Enumerable.Repeat(oldLine, 30_000));
        var originalLength = new FileInfo(logPath).Length;

        var diagnostics = new FileHotkeyDiagnostics(logPath);
        diagnostics.Log(new HotkeyDiagnosticWrite(
            Event: "detected",
            Gesture: "DoubleShift",
            Accepted: "true",
            Reason: "second_shift_after_missed_keyup"));

        var trimmedLog = File.ReadAllText(logPath);
        Assert.True(new FileInfo(logPath).Length < originalLength);
        Assert.Contains("LOG_RETENTION trimmed=true", trimmedLog);
        Assert.Contains("HOTKEY event=detected", trimmedLog);
        Assert.DoesNotContain("reason=already_down", trimmedLog);
    }
}
