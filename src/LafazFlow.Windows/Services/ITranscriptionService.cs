namespace LafazFlow.Windows.Services;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(
        string whisperCliPath,
        string modelPath,
        string audioPath,
        string initialPrompt,
        int threads,
        WhisperDecodeOptions decodeOptions,
        CancellationToken cancellationToken);
}
