namespace LafazFlow.Windows.Services;

public enum TranscriptionRecoveryAction
{
    None,
    RetryWorker,
    RetryCli
}

public static class TranscriptionRecoveryPolicy
{
    public static TranscriptionRecoveryAction Decide(
        string? failureKind,
        bool userCancelled,
        bool deliveryCommitted)
    {
        if (deliveryCommitted || userCancelled)
        {
            return TranscriptionRecoveryAction.None;
        }

        if (failureKind is null)
        {
            return TranscriptionRecoveryAction.None;
        }

        return failureKind switch
        {
            "aborted"
                or "worker_unavailable"
                or "worker_timeout"
                or "worker_busy"
                or "worker_invalidrequest"
                or "worker_internalerror"
                or "worker_invalidresponse"
                or "pipe_broken" => TranscriptionRecoveryAction.RetryWorker,
            "invalid_audio"
                or "model_missing"
                or "vad_missing"
                or "invalid_settings" => TranscriptionRecoveryAction.None,
            _ => TranscriptionRecoveryAction.None
        };
    }
}
