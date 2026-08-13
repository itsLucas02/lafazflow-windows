using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class PerformanceHealthMonitorTests
{
    [Fact]
    public void BaselineBuildsAfterTenEligibleSamplesAndExcludesOthers()
    {
        var monitor = new PerformanceHealthMonitor();
        var fingerprint = "F1";
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        monitor.Record(Sample(fingerprint, 300, 10000, cold: true));
        monitor.Record(Sample(fingerprint, 300, 10000, retried: true));
        monitor.Record(Sample(fingerprint, 300, 500));

        Assert.Equal(10, monitor.RecentSamples(fingerprint).Count);
        Assert.False(monitor.IsSlow(fingerprint, Sample(fingerprint, 300, 10000)));
    }

    [Fact]
    public void SlowClassificationRequiresBothInferenceAndRtfRules()
    {
        var monitor = new PerformanceHealthMonitor();
        var fingerprint = "F2";
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        Assert.True(monitor.IsSlow(fingerprint, Sample(fingerprint, 1100, 10000)));
        Assert.False(monitor.IsSlow(fingerprint, Sample(fingerprint, 1000, 10000)));
        Assert.False(monitor.IsSlow(fingerprint, Sample(fingerprint, 1100, 100000)));
    }

    [Fact]
    public void IsolatedOutlierDoesNotTriggerDegradation()
    {
        var triggered = 0;
        var monitor = new PerformanceHealthMonitor();
        monitor.SustainedDegradation += _ => triggered++;
        var fingerprint = "F3";
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        monitor.Record(Sample(fingerprint, 3000, 10000));

        Assert.Equal(0, triggered);
    }

    [Fact]
    public void ThreeOfFiveSlowTriggersOncePerCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var triggered = 0;
        var options = new PerformanceHealthMonitorOptions
        {
            RestartCooldown = TimeSpan.FromMinutes(10)
        };
        var monitor = new PerformanceHealthMonitor(options, () => now);
        monitor.SustainedDegradation += _ => triggered++;
        var fingerprint = "F4";
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        for (var i = 0; i < 5; i++)
        {
            monitor.Record(Sample(fingerprint, 3000, 10000));
        }

        Assert.Equal(1, triggered);

        monitor.Record(Sample(fingerprint, 3000, 10000));
        Assert.Equal(1, triggered);

        now = now.AddMinutes(11);
        for (var i = 0; i < 5; i++)
        {
            monitor.Record(Sample(fingerprint, 3000, 10000));
        }

        Assert.Equal(2, triggered);
    }

    [Fact]
    public void NormalDistributionNeverTriggersRestart()
    {
        var triggered = 0;
        var monitor = new PerformanceHealthMonitor();
        monitor.SustainedDegradation += _ => triggered++;
        var fingerprint = "F5";
        for (var i = 0; i < 40; i++)
        {
            var jitter = (i % 5) * 40L;
            monitor.Record(Sample(fingerprint, 250 + jitter, 10000));
        }

        Assert.Equal(0, triggered);
    }

    [Fact]
    public void InjectedSustainedDelayTriggersOneRecovery()
    {
        var now = DateTimeOffset.UtcNow;
        var triggered = 0;
        var options = new PerformanceHealthMonitorOptions
        {
            RestartCooldown = TimeSpan.FromMinutes(10)
        };
        var monitor = new PerformanceHealthMonitor(options, () => now);
        monitor.SustainedDegradation += _ => triggered++;
        var fingerprint = "F6";
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        for (var i = 0; i < 5; i++)
        {
            monitor.Record(Sample(fingerprint, 3000, 10000));
        }

        Assert.Equal(1, triggered);
    }

    [Fact]
    public void DiagnosticSamplesRetainColdAndRetriedRunsForReporting()
    {
        var monitor = new PerformanceHealthMonitor();
        var fingerprint = "F7";
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        monitor.Record(Sample(fingerprint, 300, 10000, cold: true));
        monitor.Record(Sample(fingerprint, 300, 10000, retried: true));
        monitor.Record(Sample(fingerprint, 300, 500));

        var diagnostics = monitor.DiagnosticSamples(fingerprint);

        Assert.Equal(13, diagnostics.Count);
        Assert.Equal(1, diagnostics.Count(sample => sample.IsCold));
        Assert.Equal(1, diagnostics.Count(sample => sample.IsRetried));
        Assert.Equal(1, diagnostics.Count(sample => sample.AudioDurationMs == 500));
        Assert.Equal(10, monitor.RecentSamples(fingerprint).Count);
    }

    private static HealthSample Sample(
        string fingerprint,
        long inferenceMs,
        long audioDurationMs,
        bool cold = false,
        bool retried = false)
    {
        return new HealthSample(
            Guid.NewGuid(),
            fingerprint,
            inferenceMs,
            audioDurationMs,
            cold,
            retried,
            false,
            false,
            DateTimeOffset.UtcNow);
    }
}
