using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class LatencyDiagnosticLogStoreTests
{
    [Fact]
    public void ParseLineParsesCompletedLatencyLine()
    {
        var row = LatencyDiagnosticLogStore.ParseLine("[2026-05-16T16:13:45.8202588+08:00] LATENCY id=abc123 status=completed model=ggml-base.en.bin threads=16 target=Cursor recording_ms=9047 queue_wait_ms=1 whisper_ms=591 paste_ms=1620 total_stop_to_done_ms=1170 total_record_to_done_ms=10218 error=none");

        Assert.NotNull(row);
        Assert.Equal("abc123", row.Id);
        Assert.Equal("completed", row.Status);
        Assert.Equal("ggml-base.en.bin", row.Model);
        Assert.Equal("16", row.Threads);
        Assert.Equal("Cursor", row.Target);
        Assert.Equal("9047", row.RecordingMs);
        Assert.Equal("1", row.QueueWaitMs);
        Assert.Equal("591", row.WhisperMs);
        Assert.Equal("1620", row.PasteMs);
        Assert.Equal("1170", row.TotalStopToDoneMs);
        Assert.Equal("10218", row.TotalRecordToDoneMs);
        Assert.Equal("none", row.Error);
    }

    [Fact]
    public void ParseLineParsesFailedLatencyLineWithUnavailableValues()
    {
        var row = LatencyDiagnosticLogStore.ParseLine("[2026-05-16T16:13:56.3366097+08:00] LATENCY id=bad456 status=failed model=ggml-base.en.bin threads=16 target=Antigravity recording_ms=6631 queue_wait_ms=0 whisper_ms=699 paste_ms=na total_stop_to_done_ms=1248 total_record_to_done_ms=7879 error=InvalidOperationException");

        Assert.NotNull(row);
        Assert.Equal("failed", row.Status);
        Assert.Equal("Antigravity", row.Target);
        Assert.Equal("na", row.PasteMs);
        Assert.Equal("InvalidOperationException", row.Error);
    }

    [Fact]
    public void ParseLineIgnoresNonLatencyLines()
    {
        var row = LatencyDiagnosticLogStore.ParseLine("[2026-05-16T16:13:56.3362316+08:00] System.InvalidOperationException: Clipboard data could not be read.");

        Assert.Null(row);
    }

    [Fact]
    public void LoadRecentReturnsNewestRowsFirstAndLimitsResults()
    {
        var logPath = CreateTempLogPath();
        File.WriteAllLines(
            logPath,
            Enumerable.Range(1, 25).Select(index =>
                $"[2026-05-16T16:{index:00}:00.0000000+08:00] LATENCY id=id{index:00} status=completed model=model.bin threads=16 target=Cursor recording_ms={index} queue_wait_ms=0 whisper_ms=1 paste_ms=2 total_stop_to_done_ms=3 total_record_to_done_ms=4 error=none"));
        var store = new LatencyDiagnosticLogStore(logPath);

        var rows = store.LoadRecent();

        Assert.Equal(20, rows.Count);
        Assert.Equal("id25", rows[0].Id);
        Assert.Equal("id06", rows[^1].Id);
    }

    [Fact]
    public void ClearLatencyLinesPreservesOtherLogLines()
    {
        var logPath = CreateTempLogPath();
        var ordinaryLine = "[2026-05-16T16:13:56.3362316+08:00] Some ordinary log line.";
        File.WriteAllLines(
            logPath,
            [
                ordinaryLine,
                "[2026-05-16T16:13:56.3366097+08:00] LATENCY id=bad456 status=failed model=ggml-base.en.bin threads=16 target=Antigravity recording_ms=6631 queue_wait_ms=0 whisper_ms=699 paste_ms=na total_stop_to_done_ms=1248 total_record_to_done_ms=7879 error=InvalidOperationException"
            ]);
        var store = new LatencyDiagnosticLogStore(logPath);

        var removed = store.ClearLatencyLines();

        Assert.Equal(1, removed);
        Assert.Equal([ordinaryLine], File.ReadAllLines(logPath));
    }

    [Fact]
    public void ClearLatencyLinesHandlesMissingLogFile()
    {
        var store = new LatencyDiagnosticLogStore(CreateTempLogPath());

        var removed = store.ClearLatencyLines();

        Assert.Equal(0, removed);
    }

    private static string CreateTempLogPath()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        return Path.Combine(root, "lafazflow.log");
    }
}
