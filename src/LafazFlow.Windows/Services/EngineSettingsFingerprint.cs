using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public static class EngineSettingsFingerprint
{
    public static string Compute(AppSettings settings)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var canonical = string.Join(
            "\n",
            settings.TranscriptionProfile.ToString(),
            settings.WhisperBackend.ToString(),
            settings.WhisperCliPath,
            settings.CudaWhisperCliPath,
            settings.ModelPath,
            settings.QualityModelPath,
            settings.VadModelPath,
            settings.EnableVad.ToString(),
            settings.WhisperThreads.ToString(CultureInfo.InvariantCulture),
            runtime.DecodeOptions.Temperature.ToString("0.0", CultureInfo.InvariantCulture),
            runtime.DecodeOptions.NoFallback.ToString(),
            runtime.DecodeOptions.SuppressNonSpeechTokens.ToString(),
            runtime.DecodeOptions.EnableVad.ToString(),
            runtime.DecodeOptions.VadModelPath,
            runtime.DecodeOptions.MaxContextTokens?.ToString(CultureInfo.InvariantCulture) ?? "");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }
}
