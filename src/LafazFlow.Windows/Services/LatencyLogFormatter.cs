using System.IO;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public static partial class LatencyLogFormatter
{
    public static string Format(LatencyTrace trace)
    {
        var finishCheckpoint = trace.Status == LatencyStatus.Failed
            ? LatencyCheckpoint.Failed
            : LatencyCheckpoint.Completed;

        return string.Join(
            " ",
            "LATENCY",
            $"id={SafeValue(trace.Id)}",
            $"status={StatusValue(trace.Status)}",
            $"model={SafeValue(SafeModelName(trace.ModelPath))}",
            $"threads={trace.Threads}",
            $"target={SafeValue(SafeTargetName(trace.TargetProcessName))}",
            Pair("recording_setup_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.RecordingStart, LatencyCheckpoint.RecordingReady)),
            Pair("recording_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.RecordingStart, LatencyCheckpoint.StopRequested)),
            Pair("stop_to_queue_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.StopRequested, LatencyCheckpoint.QueueEnqueued)),
            Pair("queue_wait_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.QueueEnqueued, LatencyCheckpoint.QueueStarted)),
            Pair("whisper_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.WhisperStarted, LatencyCheckpoint.WhisperFinished)
                ?? trace.ElapsedMilliseconds(LatencyCheckpoint.QueueStarted, LatencyCheckpoint.WhisperFinished)),
            Pair("post_process_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.PostProcessingStarted, LatencyCheckpoint.PostProcessingFinished)
                ?? trace.ElapsedMilliseconds(LatencyCheckpoint.WhisperFinished, LatencyCheckpoint.PostProcessingFinished)),
            Pair("ui_update_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.UiUpdateStarted, LatencyCheckpoint.UiUpdateFinished)
                ?? trace.ElapsedMilliseconds(LatencyCheckpoint.PostProcessingFinished, LatencyCheckpoint.UiUpdateFinished)),
            Pair("paste_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.PasteStarted, LatencyCheckpoint.PasteFinished)
                ?? trace.ElapsedMilliseconds(LatencyCheckpoint.UiUpdateFinished, LatencyCheckpoint.PasteFinished)),
            Pair("cleanup_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.CleanupStarted, LatencyCheckpoint.CleanupFinished)
                ?? trace.ElapsedMilliseconds(LatencyCheckpoint.PasteFinished, LatencyCheckpoint.CleanupFinished)),
            Pair("total_stop_to_done_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.StopRequested, finishCheckpoint)),
            Pair("total_record_to_done_ms", trace.ElapsedMilliseconds(LatencyCheckpoint.RecordingStart, finishCheckpoint)),
            $"error={SafeValue(trace.Status == LatencyStatus.Failed ? trace.ErrorKind : "none")}");
    }

    private static string Pair(string key, long? value)
    {
        return $"{key}={(value.HasValue ? value.Value.ToString() : "na")}";
    }

    private static string StatusValue(LatencyStatus status)
    {
        return status switch
        {
            LatencyStatus.Completed => "completed",
            LatencyStatus.Failed => "failed",
            _ => "running"
        };
    }

    private static string SafeModelName(string modelPath)
    {
        return string.IsNullOrWhiteSpace(modelPath)
            ? "unknown"
            : Path.GetFileName(modelPath);
    }

    private static string SafeTargetName(string? targetProcessName)
    {
        if (string.IsNullOrWhiteSpace(targetProcessName))
        {
            return "unknown";
        }

        return Path.GetFileNameWithoutExtension(targetProcessName);
    }

    private static string SafeValue(string value)
    {
        var normalized = UnsafeCharacterRegex().Replace(value.Trim(), "_");
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    [GeneratedRegex(@"[^A-Za-z0-9_.-]")]
    private static partial Regex UnsafeCharacterRegex();
}
