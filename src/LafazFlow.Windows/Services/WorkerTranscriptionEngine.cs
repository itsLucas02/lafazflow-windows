using System.Text;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class WorkerTranscriptionEngine : ITranscriptionEngine
{
    private readonly WhisperWorkerSupervisor _supervisor;

    public WorkerTranscriptionEngine(WhisperWorkerSupervisor supervisor)
    {
        _supervisor = supervisor;
    }

    public async Task<TranscriptionEngineResult> TranscribeAsync(
        string audioPath,
        AppSettings settings,
        Guid dictationId,
        CancellationToken cancellationToken)
    {
        var wav = WavPcmReader.Read(audioPath);
        if (wav is null)
        {
            return new TranscriptionEngineResult("", false, "invalid_audio", null, null);
        }

        var session = await _supervisor.GetReadySessionAsync(settings, cancellationToken);
        var response = await session.TranscribeFinalAsync(wav.Pcm, wav.SampleCount, cancellationToken);
        return response.Status switch
        {
            WhisperPipeStatus.Ok => new TranscriptionEngineResult(
                WhisperCliTranscriptionService.CleanTranscript(Encoding.UTF8.GetString(response.Data)),
                true,
                null,
                null,
                null),
            WhisperPipeStatus.Aborted => new TranscriptionEngineResult("", false, "aborted", null, null),
            WhisperPipeStatus.Unavailable => new TranscriptionEngineResult("", false, "worker_unavailable", null, null),
            _ => new TranscriptionEngineResult("", false, $"worker_{response.Status.ToString().ToLowerInvariant()}", null, null)
        };
    }
}
