using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class HotkeyDiagnosticLogStoreTests
{
    [Fact]
    public void ParseLineReadsHotkeyFields()
    {
        var row = HotkeyDiagnosticLogStore.ParseLine(
            "[2026-06-19T18:42:10.1234567+08:00] HOTKEY event=dispatched gesture=DoubleShift accepted=true state=Recording dispatch_ms=37 reason=second_shift target=Cursor");

        Assert.NotNull(row);
        Assert.Equal("dispatched", row.Event);
        Assert.Equal("DoubleShift", row.Gesture);
        Assert.Equal("true", row.Accepted);
        Assert.Equal("Recording", row.State);
        Assert.Equal("37", row.DispatchMs);
        Assert.Equal("second_shift", row.Reason);
        Assert.Equal("Cursor", row.Target);
    }

    [Fact]
    public void ParseLineIgnoresLatencyRows()
    {
        Assert.Null(HotkeyDiagnosticLogStore.ParseLine(
            "[2026-06-19T18:42:10.1234567+08:00] LATENCY id=abc status=completed"));
    }

    [Fact]
    public void LoadRecentReturnsNewestRowsFirstAndLimitsResults()
    {
        var logPath = CreateTempLogPath();
        File.WriteAllLines(
            logPath,
            Enumerable.Range(1, 25).Select(index =>
                $"[2026-06-19T18:{index:00}:00.0000000+08:00] HOTKEY event=toggle_start gesture=DoubleShift accepted=true state=Idle dispatch_ms={index} reason=second_shift target=Cursor"));
        var store = new HotkeyDiagnosticLogStore(logPath);

        var rows = store.LoadRecent();

        Assert.Equal(20, rows.Count);
        Assert.Equal("25", rows[0].DispatchMs);
        Assert.Equal("6", rows[^1].DispatchMs);
    }

    [Fact]
    public void ClearHotkeyLinesPreservesOtherLogLines()
    {
        var logPath = CreateTempLogPath();
        var ordinaryLine = "[2026-06-19T18:42:10.1234567+08:00] Ordinary log.";
        var latencyLine = "[2026-06-19T18:42:10.1234567+08:00] LATENCY id=abc status=completed";
        File.WriteAllLines(
            logPath,
            [
                ordinaryLine,
                latencyLine,
                "[2026-06-19T18:42:10.1234567+08:00] HOTKEY event=toggle_stop gesture=DoubleShift accepted=true state=Recording dispatch_ms=12 reason=second_shift target=Cursor"
            ]);
        var store = new HotkeyDiagnosticLogStore(logPath);

        var removed = store.ClearHotkeyLines();

        Assert.Equal(1, removed);
        Assert.Equal([ordinaryLine, latencyLine], File.ReadAllLines(logPath));
    }

    [Fact]
    public void ClearHotkeyLinesHandlesMissingLogFile()
    {
        var store = new HotkeyDiagnosticLogStore(CreateTempLogPath());

        var removed = store.ClearHotkeyLines();

        Assert.Equal(0, removed);
    }

    private static string CreateTempLogPath()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        return Path.Combine(root, "lafazflow.log");
    }
}
