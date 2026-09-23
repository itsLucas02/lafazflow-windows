using System.Text;
using System.IO;
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
        try
        {
            var wav = WavPcmReader.Read(audioPath);
            if (wav is null)
            {
                return new TranscriptionEngineResult("", false, "invalid_audio", null, null);
            }

            var session = await _supervisor.GetReadySessionAsync(settings, cancellationToken);
            await session.GetBackendAsync(cancellationToken);
            if (!WhisperBackendPolicy.IsWorkerCompatible(
                    settings,
                    session.CompiledBackend,
                    session.RuntimeBackend))
            {
                return new TranscriptionEngineResult("", false, "worker_backend_mismatch", null, null);
            }

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
                _ => new TranscriptionEngineResult(
                    "",
                    false,
                    $"worker_{response.Status.ToString().ToLowerInvariant()}",
                    null,
                    null)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new TranscriptionEngineResult("", false, "worker_timeout", null, null);
        }
        catch (InvalidOperationException)
        {
            return new TranscriptionEngineResult("", false, "worker_unavailable", null, null);
        }
        catch (EndOfStreamException)
        {
            return new TranscriptionEngineResult("", false, "pipe_broken", null, null);
        }
        catch (Exception)
        {
            return new TranscriptionEngineResult("", false, "worker_unavailable", null, null);
        }
    }
}
