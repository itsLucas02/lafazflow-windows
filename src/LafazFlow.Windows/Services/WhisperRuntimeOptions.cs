namespace LafazFlow.Windows.Services;

public sealed record WhisperRuntimeOptions(
    string CliPath,
    string ModelPath,
    WhisperDecodeOptions DecodeOptions);
