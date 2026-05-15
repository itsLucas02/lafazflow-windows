using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class DoubleShiftDetectorTests
{
    [Fact]
    public void SingleShiftDoesNotTrigger()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        var triggered = detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);

        Assert.False(triggered);
    }

    [Fact]
    public void SecondKeyDownInsideWindowTriggers()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        detector.RegisterKeyUp();
        var triggered = detector.RegisterKeyDown(DateTimeOffset.UnixEpoch.AddMilliseconds(300), isRepeat: false);

        Assert.True(triggered);
    }

    [Fact]
    public void KeyRepeatDoesNotTrigger()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        var triggered = detector.RegisterKeyDown(DateTimeOffset.UnixEpoch.AddMilliseconds(50), isRepeat: true);

        Assert.False(triggered);
    }

    [Fact]
    public void TwoKeyDownsOutsideWindowDoesNotTrigger()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        detector.RegisterKeyUp();
        var triggered = detector.RegisterKeyDown(DateTimeOffset.UnixEpoch.AddMilliseconds(500), isRepeat: false);

        Assert.False(triggered);
    }
}
