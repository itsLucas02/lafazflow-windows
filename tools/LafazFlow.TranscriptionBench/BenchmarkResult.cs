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
    string? Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}
