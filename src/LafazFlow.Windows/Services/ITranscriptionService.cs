namespace LafazFlow.Windows.Services;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(
        string whisperCliPath,
        string modelPath,
        string audioPath,
        string initialPrompt,
        int threads,
        CancellationToken cancellationToken);
}
