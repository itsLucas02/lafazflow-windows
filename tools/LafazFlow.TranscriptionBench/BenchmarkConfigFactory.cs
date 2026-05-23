using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.TranscriptionBench;

public static class BenchmarkConfigFactory
{
    public static IReadOnlyList<BenchmarkTranscriptionConfig> Build(
        AppSettings settings,
        IReadOnlySet<string>? configFilter)
    {
        var configs = new[]
        {
            BuildConfig("current-settings", settings),
            BuildConfig("fast-cpu-base-en", settings with
            {
                TranscriptionProfile = TranscriptionProfile.Fast,
                WhisperBackend = WhisperBackend.Cpu,
                ModelPath = settings.ModelPath
            }),
            BuildConfig("quality-cpu-q5", settings with
            {
                TranscriptionProfile = TranscriptionProfile.Quality,
                WhisperBackend = WhisperBackend.Cpu,
                EnableVad = false
            }),
            BuildConfig("quality-cuda-q5-vad", settings with
            {
                TranscriptionProfile = TranscriptionProfile.Quality,
                WhisperBackend = WhisperBackend.Cuda,
                EnableVad = true
            }),
            BuildMacOsLikeConfig(settings)
        };

        if (configFilter is null || configFilter.Count == 0)
        {
            return configs;
        }

        return configs
            .Where(config => configFilter.Contains(config.Name))
            .ToArray();
    }

    private static BenchmarkTranscriptionConfig BuildConfig(string name, AppSettings settings)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        return new BenchmarkTranscriptionConfig(
            name,
            settings,
            runtime,
            WhisperCliTranscriptionService.ValidatePaths(runtime.CliPath, runtime.ModelPath, runtime.DecodeOptions));
    }

    private static BenchmarkTranscriptionConfig BuildMacOsLikeConfig(AppSettings settings)
    {
        var macOsLikeSettings = settings with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cpu,
            EnableVad = false
        };
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(macOsLikeSettings) with
        {
            DecodeOptions = WhisperDecodeOptions.MacOsLike
        };

        return new BenchmarkTranscriptionConfig(
            "macos-like-q5",
            macOsLikeSettings,
            runtime,
            WhisperCliTranscriptionService.ValidatePaths(runtime.CliPath, runtime.ModelPath, runtime.DecodeOptions));
    }
}
