using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.TranscriptionBench;

public sealed record BenchmarkTranscriptionConfig(
    string Name,
    AppSettings Settings,
    WhisperRuntimeOptions Runtime,
    string? SkipReason)
{
    public bool IsSkipped => !string.IsNullOrWhiteSpace(SkipReason);
}
