using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class DiagnosticLogParsingTests
{
    [Fact]
    public void HotkeyLineWithInterleavedDuplicateKeysParsesFirstValues()
    {
        var line =
            "[2026-10-20T00:42:41.2547881+08:00] HOTKEY event=toggle_start gesture=DoubleShift accepted=true "
            + "state=Recording dispatch_ms=na reason=capture_started targe"
            + "[2026-10-20T00:42:41.2554545+08:00] WORKER state=ready pid=5648 fingerprint=13AFBCF9EC00";

        var parsed = HotkeyDiagnosticLogStore.ParseLine(line);

        Assert.NotNull(parsed);
        Assert.Equal("toggle_start", parsed!.Event);
        Assert.Equal("Recording", parsed.State);
    }

    [Fact]
    public void LatencyLineWithDuplicateKeysParsesFirstValues()
    {
        var line =
            "[2026-10-20T00:42:41+08:00] LATENCY id=abc status=completed model=x threads=16 "
            + "target=WindowsTerminal total_stop_to_done_ms=700 id=def status=failed";

        var parsed = LatencyDiagnosticLogStore.ParseLine(line);

        Assert.NotNull(parsed);
        Assert.Equal("abc", parsed!.Id);
        Assert.Equal("completed", parsed.Status);
    }
}
