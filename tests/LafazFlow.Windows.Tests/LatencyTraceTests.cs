using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class LatencyTraceTests
{
    [Fact]
    public void SummaryIncludesExpectedDurations()
    {
        var now = 0L;
        var trace = new LatencyTrace(() => now, timestampFrequency: 1000)
        {
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin",
            Threads = 16,
            TargetProcessName = "Cursor"
        };

        trace.Mark(LatencyCheckpoint.RecordingStart);
        now += 25;
        trace.Mark(LatencyCheckpoint.RecordingReady);
        now += 1200;
        trace.Mark(LatencyCheckpoint.StopRequested);
        now += 10;
        trace.Mark(LatencyCheckpoint.QueueEnqueued);
        now += 40;
        trace.Mark(LatencyCheckpoint.QueueStarted);
        now += 500;
        trace.Mark(LatencyCheckpoint.WhisperFinished);
        now += 20;
        trace.Mark(LatencyCheckpoint.PostProcessingFinished);
        now += 30;
        trace.Mark(LatencyCheckpoint.UiUpdateFinished);
        now += 200;
        trace.Mark(LatencyCheckpoint.PasteFinished);
        now += 5;
        trace.Mark(LatencyCheckpoint.CleanupFinished);
        trace.Complete();

        var summary = LatencyLogFormatter.Format(trace);

        Assert.Contains("LATENCY", summary);
        Assert.Contains("status=completed", summary);
        Assert.Contains("model=ggml-base.en.bin", summary);
        Assert.Contains("threads=16", summary);
        Assert.Contains("target=Cursor", summary);
        Assert.Contains("recording_setup_ms=25", summary);
        Assert.Contains("recording_ms=1225", summary);
        Assert.Contains("stop_to_queue_ms=10", summary);
        Assert.Contains("queue_wait_ms=40", summary);
        Assert.Contains("whisper_ms=500", summary);
        Assert.Contains("post_process_ms=20", summary);
        Assert.Contains("ui_update_ms=30", summary);
        Assert.Contains("paste_ms=200", summary);
        Assert.Contains("cleanup_ms=5", summary);
        Assert.Contains("total_stop_to_done_ms=805", summary);
        Assert.Contains("total_record_to_done_ms=2030", summary);
    }

    [Fact]
    public void SummaryIsPrivacySafe()
    {
        var now = 0L;
        var trace = new LatencyTrace(() => now, timestampFrequency: 1000)
        {
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin",
            TargetProcessName = @"C:\SensitiveFolder\Cursor.exe",
            Threads = 4
        };

        trace.Mark(LatencyCheckpoint.RecordingStart);
        now += 10;
        trace.Mark(LatencyCheckpoint.StopRequested);
        now += 20;
        trace.Mark(LatencyCheckpoint.Failed);
        trace.Fail(new InvalidOperationException("Transcript contained private words from the user."));

        var summary = LatencyLogFormatter.Format(trace);

        Assert.Contains("status=failed", summary);
        Assert.Contains("model=ggml-base.en.bin", summary);
        Assert.Contains("target=Cursor", summary);
        Assert.Contains("error=InvalidOperationException", summary);
        Assert.DoesNotContain(@"C:\Models\whisper", summary);
        Assert.DoesNotContain(@"C:\SensitiveFolder", summary);
        Assert.DoesNotContain("private words", summary);
    }

    [Fact]
    public void MissingOptionalCheckpointsAreLoggedAsUnavailable()
    {
        var trace = new LatencyTrace(() => 0, timestampFrequency: 1000);
        trace.Mark(LatencyCheckpoint.RecordingStart);
        trace.Complete();

        var summary = LatencyLogFormatter.Format(trace);

        Assert.Contains("queue_wait_ms=na", summary);
        Assert.Contains("paste_ms=na", summary);
    }
}
