namespace LafazFlow.TranscriptionBench;

public sealed record RecordingFixture(
    string Id,
    string AudioPath,
    string ExpectedText,
    DateTime LastWriteTimeUtc);
