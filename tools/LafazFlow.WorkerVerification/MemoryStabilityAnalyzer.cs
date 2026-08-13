namespace LafazFlow.WorkerVerification;

public enum MemoryStabilityVerdict
{
    Stable,
    Growing,
    Uncertain
}

public sealed record MemoryCheckpoint(
    int RequestIndex,
    long WorkingSetBytes,
    long? VramMiB);

public sealed record MemoryStabilityResult(
    MemoryStabilityVerdict Verdict,
    string Reason,
    long WorkingSetGrowthBytes,
    long? VramGrowthMiB);

/// <summary>
/// Evidence-based steady-state memory rule for the native/CUDA worker:
/// memory is Stable when post-warmup working-set and VRAM growth stay within
/// documented tolerances at the end of the run and at every captured checkpoint.
/// A single checkpoint above tolerance counts as growing (spikes are evidence
/// of unsteady allocation, not noise to hide). Negative growth is fine.
/// </summary>
public static class MemoryStabilityAnalyzer
{
    public static long Growth(long baseline, long current)
    {
        return current - baseline;
    }

    /// <summary>
    /// Verification exit policy: only a Stable verdict passes. Growing and
    /// Uncertain both fail closed so missing checkpoint data can never be
    /// mistaken for proof of stability.
    /// </summary>
    public static bool PassesVerificationGate(MemoryStabilityVerdict verdict)
    {
        return verdict == MemoryStabilityVerdict.Stable;
    }

    public static MemoryStabilityResult Classify(
        long workingSetBaselineBytes,
        long workingSetFinalBytes,
        long? vramBaselineMiB,
        long? vramFinalMiB,
        long workingSetToleranceBytes,
        long vramToleranceMiB,
        IReadOnlyList<MemoryCheckpoint> checkpoints)
    {
        var workingSetGrowth = Growth(workingSetBaselineBytes, workingSetFinalBytes);
        long? vramGrowth = vramBaselineMiB.HasValue && vramFinalMiB.HasValue
            ? vramFinalMiB.Value - vramBaselineMiB.Value
            : null;

        if (checkpoints.Count == 0)
        {
            return new MemoryStabilityResult(
                MemoryStabilityVerdict.Uncertain,
                "No checkpoint data was captured during the measured run.",
                workingSetGrowth,
                vramGrowth);
        }

        var workingSetCheckpointExceeded = checkpoints.Any(checkpoint =>
            Growth(workingSetBaselineBytes, checkpoint.WorkingSetBytes) > workingSetToleranceBytes);
        var vramCheckpointExceeded = checkpoints.Any(checkpoint =>
            vramBaselineMiB.HasValue
            && checkpoint.VramMiB.HasValue
            && checkpoint.VramMiB.Value - vramBaselineMiB.Value > vramToleranceMiB);

        var reasons = new List<string>();
        if (workingSetGrowth > workingSetToleranceBytes)
        {
            reasons.Add(
                $"working set grew {workingSetGrowth} bytes after warmup (tolerance {workingSetToleranceBytes} bytes)");
        }

        if (workingSetCheckpointExceeded)
        {
            reasons.Add("a working-set checkpoint exceeded tolerance during the run");
        }

        if (vramGrowth is { } finalVramGrowth && finalVramGrowth > vramToleranceMiB)
        {
            reasons.Add($"VRAM grew {finalVramGrowth} MiB after warmup (tolerance {vramToleranceMiB} MiB)");
        }

        if (vramCheckpointExceeded)
        {
            reasons.Add("a VRAM checkpoint exceeded tolerance during the run");
        }

        if (reasons.Count > 0)
        {
            return new MemoryStabilityResult(
                MemoryStabilityVerdict.Growing,
                string.Join("; ", reasons),
                workingSetGrowth,
                vramGrowth);
        }

        var vramText = vramGrowth.HasValue
            ? $"{vramGrowth.Value} MiB (tolerance {vramToleranceMiB} MiB)"
            : "n/a (nvidia-smi unavailable)";
        return new MemoryStabilityResult(
            MemoryStabilityVerdict.Stable,
            $"Post-warmup growth within tolerance: working set {workingSetGrowth} bytes (tolerance {workingSetToleranceBytes} bytes), VRAM {vramText}; all checkpoints stable.",
            workingSetGrowth,
            vramGrowth);
    }
}
