using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

/// <summary>
/// Decides which backend a given configuration requires and whether a worker's
/// reported compiled/runtime backends can satisfy it. Backend mismatches are
/// never silently downgraded: CUDA settings must run on a CUDA worker.
/// </summary>
public static class WhisperBackendPolicy
{
    public static WhisperBackend RequiredBackend(AppSettings settings)
    {
        return settings.TranscriptionProfile == TranscriptionProfile.Quality
            && settings.WhisperBackend == WhisperBackend.Cuda
            ? WhisperBackend.Cuda
            : WhisperBackend.Cpu;
    }

    public static bool IsWorkerCompatible(
        AppSettings settings,
        WhisperBackend? compiledBackend,
        WhisperBackend? runtimeBackend)
    {
        var required = RequiredBackend(settings);
        if (required == WhisperBackend.Cuda)
        {
            return compiledBackend == WhisperBackend.Cuda && runtimeBackend == WhisperBackend.Cuda;
        }

        return runtimeBackend == WhisperBackend.Cpu;
    }
}
