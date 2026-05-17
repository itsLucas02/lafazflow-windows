namespace LafazFlow.Windows.Services;

public sealed record WhisperDecodeOptions(
    double Temperature,
    bool NoFallback,
    bool SuppressNonSpeechTokens,
    bool EnableVad,
    string VadModelPath)
{
    public static WhisperDecodeOptions Fast { get; } = new(
        Temperature: 0,
        NoFallback: true,
        SuppressNonSpeechTokens: false,
        EnableVad: false,
        VadModelPath: "");

    public static WhisperDecodeOptions QualityWithVad(string vadModelPath) => new(
        Temperature: 0,
        NoFallback: true,
        SuppressNonSpeechTokens: true,
        EnableVad: true,
        VadModelPath: vadModelPath);
}
