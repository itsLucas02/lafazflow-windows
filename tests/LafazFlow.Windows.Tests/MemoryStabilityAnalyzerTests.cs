using LafazFlow.WorkerVerification;

namespace LafazFlow.Windows.Tests;

public sealed class MemoryStabilityAnalyzerTests
{
    private const long WorkingSetToleranceBytes = 64 * 1024 * 1024;
    private const long VramToleranceMiB = 64;

    [Fact]
    public void GrowthComputesFinalMinusBaseline()
    {
        Assert.Equal(100, MemoryStabilityAnalyzer.Growth(200, 300));
        Assert.Equal(-50, MemoryStabilityAnalyzer.Growth(300, 250));
    }

    [Fact]
    public void StableWhenPostWarmupGrowthAndCheckpointsStayWithinTolerance()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            workingSetBaselineBytes: 400L * 1024 * 1024,
            workingSetFinalBytes: 410L * 1024 * 1024,
            vramBaselineMiB: 1000,
            vramFinalMiB: 1004,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            [
                new MemoryCheckpoint(25, 405L * 1024 * 1024, 1002),
                new MemoryCheckpoint(50, 408L * 1024 * 1024, 1003),
                new MemoryCheckpoint(100, 410L * 1024 * 1024, 1004)
            ]);

        Assert.Equal(MemoryStabilityVerdict.Stable, result.Verdict);
        Assert.Equal(10L * 1024 * 1024, result.WorkingSetGrowthBytes);
        Assert.Equal(4, result.VramGrowthMiB);
    }

    [Fact]
    public void NegativeGrowthIsStable()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            400L * 1024 * 1024,
            395L * 1024 * 1024,
            1000,
            998,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            [new MemoryCheckpoint(100, 396L * 1024 * 1024, 999)]);

        Assert.Equal(MemoryStabilityVerdict.Stable, result.Verdict);
        Assert.Equal(-5L * 1024 * 1024, result.WorkingSetGrowthBytes);
    }

    [Fact]
    public void GrowingWhenFinalWorkingSetExceedsTolerance()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            400L * 1024 * 1024,
            500L * 1024 * 1024,
            1000,
            1000,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            [new MemoryCheckpoint(100, 500L * 1024 * 1024, 1000)]);

        Assert.Equal(MemoryStabilityVerdict.Growing, result.Verdict);
        Assert.Contains("working set grew", result.Reason);
    }

    [Fact]
    public void GrowingWhenVramExceedsTolerance()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            400L * 1024 * 1024,
            405L * 1024 * 1024,
            1000,
            1200,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            [new MemoryCheckpoint(100, 405L * 1024 * 1024, 1200)]);

        Assert.Equal(MemoryStabilityVerdict.Growing, result.Verdict);
        Assert.Contains("VRAM grew", result.Reason);
    }

    [Fact]
    public void GrowingWhenCheckpointSpikesAboveToleranceEvenIfFinalIsFlat()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            400L * 1024 * 1024,
            401L * 1024 * 1024,
            1000,
            1001,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            [new MemoryCheckpoint(25, 600L * 1024 * 1024, 1001)]);

        Assert.Equal(MemoryStabilityVerdict.Growing, result.Verdict);
        Assert.Contains("checkpoint exceeded tolerance", result.Reason);
    }

    [Fact]
    public void UncertainWhenNoCheckpointsCaptured()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            400L * 1024 * 1024,
            402L * 1024 * 1024,
            1000,
            1001,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            []);

        Assert.Equal(MemoryStabilityVerdict.Uncertain, result.Verdict);
    }

    [Fact]
    public void StableOnWorkingSetWhenVramDataUnavailable()
    {
        var result = MemoryStabilityAnalyzer.Classify(
            400L * 1024 * 1024,
            405L * 1024 * 1024,
            null,
            null,
            WorkingSetToleranceBytes,
            VramToleranceMiB,
            [new MemoryCheckpoint(100, 405L * 1024 * 1024, null)]);

        Assert.Equal(MemoryStabilityVerdict.Stable, result.Verdict);
        Assert.Null(result.VramGrowthMiB);
        Assert.Contains("nvidia-smi unavailable", result.Reason);
    }
}
