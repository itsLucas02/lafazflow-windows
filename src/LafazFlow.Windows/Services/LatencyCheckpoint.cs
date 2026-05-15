namespace LafazFlow.Windows.Services;

public enum LatencyCheckpoint
{
    RecordingStart,
    RecordingReady,
    StopRequested,
    QueueEnqueued,
    QueueStarted,
    WhisperStarted,
    WhisperFinished,
    PostProcessingStarted,
    PostProcessingFinished,
    UiUpdateStarted,
    UiUpdateFinished,
    PasteStarted,
    PasteFinished,
    CleanupStarted,
    CleanupFinished,
    Completed,
    Failed
}
