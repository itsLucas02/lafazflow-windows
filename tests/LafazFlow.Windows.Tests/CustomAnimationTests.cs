using System.Windows;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class CustomAnimationTests
{
    [Fact]
    public void CornerRadiusAnimationReturnsTargetForUnexpectedOrigin()
    {
        var animation = new CornerRadiusAnimation { To = new CornerRadius(20) };

        var value = Assert.IsType<CornerRadius>(animation.GetCurrentValue("bad origin", new CornerRadius(0), animationClock: null!));

        Assert.Equal(new CornerRadius(20), value);
    }

    [Fact]
    public void CornerRadiusAnimationReturnsTargetForInvalidOrigin()
    {
        var animation = new CornerRadiusAnimation { To = new CornerRadius(20) };

        var value = Assert.IsType<CornerRadius>(
            animation.GetCurrentValue(new CornerRadius(double.NaN), new CornerRadius(0), animationClock: null!));

        Assert.Equal(new CornerRadius(20), value);
    }

    [Fact]
    public void GridLengthAnimationReturnsTargetForUnexpectedOrigin()
    {
        var animation = new GridLengthAnimation { To = new GridLength(56) };

        var value = Assert.IsType<GridLength>(animation.GetCurrentValue("bad origin", new GridLength(0), animationClock: null!));

        Assert.Equal(56, value.Value);
        Assert.True(value.IsAbsolute);
    }

    [Fact]
    public void GridLengthAnimationReturnsTargetForNonPixelOrigin()
    {
        var animation = new GridLengthAnimation { To = new GridLength(56) };

        var value = Assert.IsType<GridLength>(
            animation.GetCurrentValue(GridLength.Auto, new GridLength(0), animationClock: null!));

        Assert.Equal(56, value.Value);
        Assert.True(value.IsAbsolute);
    }
}
