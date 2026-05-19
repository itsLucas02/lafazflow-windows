using System.Windows;
using System.Windows.Media.Animation;

namespace LafazFlow.Windows.UI;

public sealed class CornerRadiusAnimation : AnimationTimeline
{
    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To),
        typeof(CornerRadius),
        typeof(CornerRadiusAnimation));

    public CornerRadius To
    {
        get => (CornerRadius)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(CornerRadius);

    protected override Freezable CreateInstanceCore()
    {
        return new CornerRadiusAnimation();
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        if (defaultOriginValue is not CornerRadius from || !IsValid(from) || !IsValid(To))
        {
            return To;
        }

        var progress = animationClock?.CurrentProgress ?? 0;
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        return new CornerRadius(
            from.TopLeft + (To.TopLeft - from.TopLeft) * progress,
            from.TopRight + (To.TopRight - from.TopRight) * progress,
            from.BottomRight + (To.BottomRight - from.BottomRight) * progress,
            from.BottomLeft + (To.BottomLeft - from.BottomLeft) * progress);
    }

    private static bool IsValid(CornerRadius radius)
    {
        return IsFinite(radius.TopLeft)
            && IsFinite(radius.TopRight)
            && IsFinite(radius.BottomRight)
            && IsFinite(radius.BottomLeft);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
