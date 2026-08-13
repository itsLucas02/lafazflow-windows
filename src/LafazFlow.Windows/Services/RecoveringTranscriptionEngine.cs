using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class RecoveringTranscriptionEngine : ITranscriptionEngine
{
    private readonly ITranscriptionEngine _primary;
    private readonly ITranscriptionEngine _fallback;
    private readonly Func<AppSettings, CancellationToken, Task> _restartAsync;

    public RecoveringTranscriptionEngine(
        ITranscriptionEngine primary,
        ITranscriptionEngine fallback,
        Func<AppSettings, CancellationToken, Task> restartAsync)
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
        if (first.Succeeded
            || TranscriptionRecoveryPolicy.Decide(first.FailureKind, false, false)
                == TranscriptionRecoveryAction.None)
        {
            return first;
        }

        await _restartAsync(settings, cancellationToken);
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
