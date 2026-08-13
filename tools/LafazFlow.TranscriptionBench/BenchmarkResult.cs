namespace LafazFlow.TranscriptionBench;

public sealed record BenchmarkResult(
    string FixtureId,
    string ConfigName,
    string ModelFileName,
    string Backend,
    long ElapsedMilliseconds,
    string ExpectedTranscript,
    string RawTranscript,
    string PostProcessedTranscript,
    double NormalizedEditDistance,
    int ExpectedKeyTermCount,
    int ActualKeyTermCount,
    IReadOnlyList<string> MissingKeyTerms,
    string? Error,
    long? AudioDurationMs = null,
    long? ProcessStartMs = null,
    long? ModelLoadMs = null,
    long? InferenceMs = null,
    long? OutputReadMs = null,
    double? RealtimeFactor = null,
    int? RawCharCount = null,
    int? FormattedCharCount = null,
    string RawFinalCharCategory = "",
    string FormattedFinalCharCategory = "",
    bool IsCold = false,
    int RepeatIndex = 0)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);

    public bool IsEmptyResult => Succeeded && string.IsNullOrWhiteSpace(PostProcessedTranscript);
}
