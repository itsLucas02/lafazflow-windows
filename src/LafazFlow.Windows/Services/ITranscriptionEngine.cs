using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public interface ITranscriptionEngine
{
    Task<TranscriptionEngineResult> TranscribeAsync(
        string audioPath,
        AppSettings settings,
        Guid dictationId,
        CancellationToken cancellationToken);
}

public sealed record TranscriptionEngineResult(
    string Text,
    bool Succeeded,
    string? FailureKind,
    long? ModelLoadMs,
    long? InferenceMs,
    bool WasRetried = false);
