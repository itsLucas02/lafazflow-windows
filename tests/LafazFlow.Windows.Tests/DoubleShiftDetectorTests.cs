using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class DoubleShiftDetectorTests
{
    [Fact]
    public void SingleShiftDoesNotTrigger()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        var triggered = detector.RegisterShiftUp(DateTimeOffset.UnixEpoch);

        Assert.False(triggered);
    }

    [Fact]
    public void TwoShiftUpsInsideWindowTriggers()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterShiftUp(DateTimeOffset.UnixEpoch);
        var triggered = detector.RegisterShiftUp(DateTimeOffset.UnixEpoch.AddMilliseconds(300));

        Assert.True(triggered);
    }

    [Fact]
    public void TwoShiftUpsOutsideWindowDoesNotTrigger()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterShiftUp(DateTimeOffset.UnixEpoch);
        var triggered = detector.RegisterShiftUp(DateTimeOffset.UnixEpoch.AddMilliseconds(500));

        Assert.False(triggered);
    }
}
