namespace LafazFlow.Windows.Services;

public sealed record WhisperDecodeOptions(
    double Temperature,
    bool SuppressNonSpeechTokens,
    bool EnableVad,
    string VadModelPath)
{
    public static WhisperDecodeOptions Fast { get; } = new(
        Temperature: 0,
        SuppressNonSpeechTokens: false,
        EnableVad: false,
        VadModelPath: "");

    public static WhisperDecodeOptions QualityWithVad(string vadModelPath) => new(
        Temperature: 0.2,
        SuppressNonSpeechTokens: true,
        EnableVad: true,
        VadModelPath: vadModelPath);
}
