namespace LafazFlow.Windows.Services;

public enum LatencyCheckpoint
{
    HotkeyReceived,
    ToggleHandlingStarted,
    RecordingStart,
    RecordingReady,
    RecorderShown,
    StopRequested,
    StopHotkeyReceived,
    QueueEnqueued,
    QueueStarted,
    PreviewStartRequested,
    PreviewStarted,
    PreviewStopRequested,
    PreviewStopped,
    WhisperStarted,
    WhisperFinished,
    PostProcessingStarted,
    PostProcessingFinished,
    UiUpdateStarted,
    UiUpdateFinished,
    PasteStarted,
    PasteFinished,
    UiHideStarted,
    UiHidden,
    CleanupStarted,
    CleanupFinished,
    Completed,
    Failed
}
