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
        now += 5;
        trace.Mark(LatencyCheckpoint.RecorderShown);
        now += 7;
        trace.Mark(LatencyCheckpoint.PreviewStartRequested);
        now += 8;
        trace.Mark(LatencyCheckpoint.PreviewStarted);
        now += 1200;
        trace.Mark(LatencyCheckpoint.StopHotkeyReceived);
        now += 3;
        trace.Mark(LatencyCheckpoint.StopRequested);
        now += 10;
        trace.Mark(LatencyCheckpoint.QueueEnqueued);
        now += 40;
        trace.Mark(LatencyCheckpoint.QueueStarted);
        now += 4;
        trace.Mark(LatencyCheckpoint.PreviewStopRequested);
        now += 9;
        trace.Mark(LatencyCheckpoint.PreviewStopped);
        trace.Mark(LatencyCheckpoint.WhisperStarted);
        now += 500;
        trace.Mark(LatencyCheckpoint.WhisperFinished);
        now += 20;
        trace.Mark(LatencyCheckpoint.PostProcessingFinished);
        now += 30;
        trace.Mark(LatencyCheckpoint.UiUpdateFinished);
        now += 200;
        trace.Mark(LatencyCheckpoint.PasteFinished);
        now += 6;
        trace.Mark(LatencyCheckpoint.UiHideStarted);
        now += 12;
        trace.Mark(LatencyCheckpoint.UiHidden);
        trace.Mark(LatencyCheckpoint.CleanupStarted);
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
        Assert.Contains("hotkey_to_visible_ms=na", summary);
        Assert.Contains("recording_ms=1248", summary);
        Assert.Contains("stop_to_queue_ms=10", summary);
        Assert.Contains("stop_hotkey_to_queue_ms=13", summary);
        Assert.Contains("queue_wait_ms=40", summary);
        Assert.Contains("preview_start_ms=8", summary);
        Assert.Contains("preview_stop_ms=9", summary);
        Assert.Contains("whisper_ms=500", summary);
        Assert.Contains("post_process_ms=20", summary);
        Assert.Contains("ui_update_ms=30", summary);
        Assert.Contains("paste_ms=200", summary);
        Assert.Contains("ui_hide_ms=12", summary);
        Assert.Contains("cleanup_ms=5", summary);
        Assert.Contains("total_stop_to_done_ms=836", summary);
        Assert.Contains("total_record_to_done_ms=2084", summary);
    }

    [Fact]
    public void SummaryIncludesHotkeyDispatchAndVisibleDurations()
    {
        var now = 0L;
        var trace = new LatencyTrace(() => now, timestampFrequency: 1000);

        trace.Mark(LatencyCheckpoint.HotkeyReceived);
        now += 11;
        trace.Mark(LatencyCheckpoint.ToggleHandlingStarted);
        now += 24;
        trace.Mark(LatencyCheckpoint.RecorderShown);
        trace.Complete();

        var summary = LatencyLogFormatter.Format(trace);

        Assert.Contains("toggle_dispatch_ms=11", summary);
        Assert.Contains("hotkey_to_visible_ms=35", summary);
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
