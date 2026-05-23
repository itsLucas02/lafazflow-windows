namespace LafazFlow.Windows.Services;

public sealed record WhisperDecodeOptions(
    double Temperature,
    bool NoFallback,
    bool SuppressNonSpeechTokens,
    bool EnableVad,
    string VadModelPath,
    int? MaxContextTokens = null)
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

    public static WhisperDecodeOptions MacOsLike { get; } = new(
        Temperature: 0.2,
        NoFallback: false,
        SuppressNonSpeechTokens: false,
        EnableVad: false,
        VadModelPath: "",
        MaxContextTokens: 0);
}
