using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class LatencyTelemetryExtensionTests
{
    [Fact]
    public void FormatterEmitsEnginePhaseAndCharacterFields()
    {
        var now = 0L;
        var trace = new LatencyTrace(() => now, timestampFrequency: 1000)
        {
            ModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            Threads = 16
        };
        trace.ModelLoadMs = 891;
        trace.InferenceMs = 1445;
        trace.ResponseTransferMs = 12;
        trace.RawCharCount = 120;
        trace.FormattedCharCount = 124;
        trace.ClipboardCharCount = 124;
        trace.RawFinalCharCategory = "letter";
        trace.FormattedFinalCharCategory = "punct";
        trace.ClipboardFinalCharCategory = "punct";
        trace.Mark(LatencyCheckpoint.AudioDrainStarted);
        now = 5;
        trace.Mark(LatencyCheckpoint.AudioDrainFinished);
        trace.Mark(LatencyCheckpoint.WaveFinalizeStarted);
        now = 9;
        trace.Mark(LatencyCheckpoint.WaveFinalizeFinished);

        var line = LatencyLogFormatter.Format(trace);

        Assert.Contains("model_load_ms=891", line);
        Assert.Contains("inference_ms=1445", line);
        Assert.Contains("response_transfer_ms=12", line);
        Assert.Contains("audio_drain_ms=5", line);
        Assert.Contains("wave_finalize_ms=4", line);
        Assert.Contains("raw_chars=120", line);
        Assert.Contains("formatted_chars=124", line);
        Assert.Contains("clipboard_chars=124", line);
        Assert.Contains("raw_final_char=letter", line);
        Assert.Contains("formatted_final_char=punct", line);
        Assert.Contains("clipboard_final_char=punct", line);
    }

    [Fact]
    public void StoreParsesNewFieldsAndToleratesOlderRows()
    {
        var trace = new LatencyTrace
        {
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin",
            Threads = 8
        };
        trace.ModelLoadMs = 300;
        trace.InferenceMs = 700;
        trace.RawCharCount = 40;
        trace.RawFinalCharCategory = "digit";

        var row = LatencyDiagnosticLogStore.ParseLine(LatencyLogFormatter.Format(trace));

        Assert.NotNull(row);
        Assert.Equal("300", row.ModelLoadMs);
        Assert.Equal("700", row.InferenceMs);
        Assert.Equal("40", row.RawChars);
        Assert.Equal("digit", row.RawFinalChar);
        Assert.Equal("na", row.ClipboardChars);

        const string olderLine =
            "[2026-08-08T12:00:00+08:00] LATENCY id=abc status=completed model=ggml-base.en.bin threads=8 " +
            "target=unknown toggle_dispatch_ms=1 hotkey_to_visible_ms=2 recording_ms=1000 stop_hotkey_to_queue_ms=10 " +
            "queue_wait_ms=5 preview_start_ms=3 preview_stop_ms=4 whisper_ms=900 paste_ms=20 ui_hide_ms=3 " +
            "total_stop_to_done_ms=1100 total_record_to_done_ms=2100 error=none";

        var olderRow = LatencyDiagnosticLogStore.ParseLine(olderLine);

        Assert.NotNull(olderRow);
        Assert.Equal("900", olderRow.WhisperMs);
        Assert.Equal("na", olderRow.ModelLoadMs);
        Assert.Equal("na", olderRow.InferenceMs);
        Assert.Equal("na", olderRow.RawChars);
        Assert.Equal("na", olderRow.ClipboardFinalChar);
    }
}
