using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class CliTranscriptionEngine : ITranscriptionEngine
{
    private readonly ITranscriptionService _transcription;
    private readonly ITranscriptionTimingProvider? _timingProvider;

    public CliTranscriptionEngine(
        ITranscriptionService transcription,
        ITranscriptionTimingProvider? timingProvider = null)
    {
        _transcription = transcription;
        _timingProvider = timingProvider;
    }

    public async Task<TranscriptionEngineResult> TranscribeAsync(
        string audioPath,
        AppSettings settings,
        Guid dictationId,
        CancellationToken cancellationToken)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(settings);
        long? modelLoadMs = null;
        long? inferenceMs = null;
        string text;

        if (_timingProvider is not null)
        {
            var timed = await _timingProvider.TranscribeWithTimingAsync(
                runtime.CliPath,
                runtime.ModelPath,
                audioPath,
                prompt,
                settings.WhisperThreads,
                runtime.DecodeOptions,
                cancellationToken);
            text = timed.Text;
            modelLoadMs = timed.Timing?.LoadMs;
            inferenceMs = timed.Timing is { } timing && (timing.EncodeMs.HasValue || timing.DecodeMs.HasValue)
                ? (timing.EncodeMs ?? 0) + (timing.DecodeMs ?? 0)
                : null;
        }
        else
        {
            text = await _transcription.TranscribeAsync(
                runtime.CliPath,
                runtime.ModelPath,
                audioPath,
                prompt,
                settings.WhisperThreads,
                runtime.DecodeOptions,
                cancellationToken);
        }

        return new TranscriptionEngineResult(
            text,
            Succeeded: true,
            FailureKind: null,
            modelLoadMs,
            inferenceMs);
    }
}
