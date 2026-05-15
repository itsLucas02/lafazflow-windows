using System.Windows;
using System.Windows.Media.Animation;

namespace LafazFlow.Windows.UI;

public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To),
        typeof(GridLength),
        typeof(GridLengthAnimation));

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var from = (GridLength)defaultOriginValue;
        var progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        var value = from.Value + (To.Value - from.Value) * progress;
        return new GridLength(value);
    }
}
