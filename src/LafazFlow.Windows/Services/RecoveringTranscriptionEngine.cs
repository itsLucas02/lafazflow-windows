using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class RecoveringTranscriptionEngine : ITranscriptionEngine
{
    private readonly ITranscriptionEngine _primary;
    private readonly ITranscriptionEngine _fallback;
    private readonly Func<AppSettings, string, CancellationToken, Task> _restartAsync;

    public RecoveringTranscriptionEngine(
        ITranscriptionEngine primary,
        ITranscriptionEngine fallback,
        Func<AppSettings, string, CancellationToken, Task> restartAsync)
    {
        _primary = primary;
        _fallback = fallback;
        _restartAsync = restartAsync;
    }

    public async Task<TranscriptionEngineResult> TranscribeAsync(
        string audioPath,
        AppSettings settings,
        Guid dictationId,
        CancellationToken cancellationToken)
    {
        var first = await _primary.TranscribeAsync(audioPath, settings, dictationId, cancellationToken);
        var action = TranscriptionRecoveryPolicy.Decide(first.FailureKind, false, false);
        if (first.Succeeded || action == TranscriptionRecoveryAction.None)
        {
            return first;
        }

        if (action == TranscriptionRecoveryAction.RetryCli)
        {
            // The worker cannot satisfy the selected backend (for example CUDA
            // settings with a CPU-compiled worker); restarting it would not
            // help, so go straight to the identical-settings CLI compatibility
            // path without retrying the worker.
            var compatCliResult = await _fallback.TranscribeAsync(audioPath, settings, dictationId, cancellationToken);
            return compatCliResult.Succeeded
                ? compatCliResult with { WasRetried = true }
                : first;
        }

        await _restartAsync(settings, first.FailureKind ?? "worker_unavailable", cancellationToken);
        var retried = await _primary.TranscribeAsync(audioPath, settings, dictationId, cancellationToken);
        if (retried.Succeeded)
        {
            return retried with { WasRetried = true };
        }

        var cliRecovery = await _fallback.TranscribeAsync(audioPath, settings, dictationId, cancellationToken);
        return cliRecovery.Succeeded
            ? cliRecovery with { WasRetried = true }
            : retried;
    }
}
