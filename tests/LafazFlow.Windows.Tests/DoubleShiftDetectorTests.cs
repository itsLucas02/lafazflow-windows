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
    public void SecondKeyDownInsideRelaxedWindowTriggers()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(500));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        detector.RegisterKeyUp();
        var triggered = detector.RegisterKeyDown(DateTimeOffset.UnixEpoch.AddMilliseconds(450), isRepeat: false);

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
    public void KeyRepeatReturnsDiagnosticReason()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        var result = detector.RegisterKeyDownWithReason(DateTimeOffset.UnixEpoch.AddMilliseconds(50), isRepeat: true);

        Assert.False(result.Triggered);
        Assert.Equal("repeat", result.Reason);
    }

    [Fact]
    public void AlreadyDownReturnsDiagnosticReason()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(350));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        var result = detector.RegisterKeyDownWithReason(DateTimeOffset.UnixEpoch.AddMilliseconds(50), isRepeat: false);

        Assert.False(result.Triggered);
        Assert.Equal("already_down", result.Reason);
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

    [Fact]
    public void StaleDownStateDoesNotBlockFutureDoubleShift()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(500));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch.AddSeconds(2), isRepeat: false);
        detector.RegisterKeyUp();
        var triggered = detector.RegisterKeyDown(DateTimeOffset.UnixEpoch.AddSeconds(2).AddMilliseconds(300), isRepeat: false);

        Assert.True(triggered);
    }

    [Fact]
    public void SecondShiftReturnsDiagnosticReason()
    {
        var detector = new DoubleShiftDetector(TimeSpan.FromMilliseconds(500));

        detector.RegisterKeyDown(DateTimeOffset.UnixEpoch, isRepeat: false);
        detector.RegisterKeyUp();
        var result = detector.RegisterKeyDownWithReason(DateTimeOffset.UnixEpoch.AddMilliseconds(300), isRepeat: false);

        Assert.True(result.Triggered);
        Assert.Equal("second_shift", result.Reason);
    }
}
