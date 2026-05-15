using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class LiveTranscriptStabilizerTests
{
    [Fact]
    public void TryAcceptNormalizesWhitespace()
    {
        var stabilizer = new LiveTranscriptStabilizer();

        Assert.True(stabilizer.TryAccept("  Testing,\r\n testing   one two. ", out var preview));

        Assert.Equal("Testing, testing one two.", preview);
    }

    [Fact]
    public void TryAcceptSuppressesExactDuplicate()
    {
        var stabilizer = new LiveTranscriptStabilizer();

        Assert.True(stabilizer.TryAccept("Testing one two.", out _));

        Assert.False(stabilizer.TryAccept(" Testing  one two. ", out var preview));
        Assert.Equal("", preview);
        Assert.Equal("duplicate", stabilizer.LastSuppressionReason);
    }

    [Fact]
    public void TryAcceptRejectsShorterRegressivePreview()
    {
        var stabilizer = new LiveTranscriptStabilizer();

        Assert.True(stabilizer.TryAccept("Testing testing one two three over.", out _));

        Assert.False(stabilizer.TryAccept("Testing testing one", out var preview));
        Assert.Equal("", preview);
        Assert.Equal("regressive", stabilizer.LastSuppressionReason);
    }

    [Fact]
    public void TryAcceptAllowsLongerContinuation()
    {
        var stabilizer = new LiveTranscriptStabilizer();

        Assert.True(stabilizer.TryAccept("Testing testing one two", out _));

        Assert.True(stabilizer.TryAccept("Testing testing one two three over.", out var preview));
        Assert.Equal("Testing testing one two three over.", preview);
    }

    [Fact]
    public void ResetAllowsSamePreviewInNewSession()
    {
        var stabilizer = new LiveTranscriptStabilizer();

        Assert.True(stabilizer.TryAccept("Testing one two.", out _));
        stabilizer.Reset();

        Assert.True(stabilizer.TryAccept("Testing one two.", out var preview));
        Assert.Equal("Testing one two.", preview);
    }
}
