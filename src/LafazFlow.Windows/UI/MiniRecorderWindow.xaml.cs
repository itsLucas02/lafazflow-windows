using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using WpfVisibility = System.Windows.Visibility;
using WpfColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace LafazFlow.Windows.UI;

public partial class MiniRecorderWindow : Window, IMiniRecorderWindow
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly WpfRectangle[] _bars;
    private readonly WpfRectangle[] _processingDots;
    private readonly ScaleTransform[] _processingDotScales;
    private readonly ScaleTransform _shellScale = new(1, 1);
    private readonly TranslateTransform _shellTranslate = new(0, 0);
    private readonly TranslateTransform _previewTranslate = new(0, 4);
    private DateTime _lastRender = DateTime.UtcNow;
    private bool _lastShowVisualizer = true;
    private bool _lastShowProcessingIndicator;
    private bool _lastHasStatusText;
    private bool _lastHasLiveTranscript;
    private int _lastProcessingPulseBucket = -1;
    private bool _isHiding;

    public MiniRecorderWindow(MiniRecorderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _bars = Enumerable.Range(0, MiniRecorderVisualSpec.BarCount).Select(_ => CreateBar()).ToArray();
        _processingDots = Enumerable.Range(0, MiniRecorderVisualSpec.ProcessingDotCount).Select(_ => CreateProcessingDot()).ToArray();
        _processingDotScales = _processingDots.Select(dot => (ScaleTransform)dot.RenderTransform).ToArray();
        RecorderShell.RenderTransformOrigin = new System.Windows.Point(0.5, 1);
        RecorderShell.RenderTransform = new TransformGroup
        {
            Children =
            {
                _shellScale,
                _shellTranslate
            }
        };
        LiveTranscriptOverlay.RenderTransform = _previewTranslate;

        foreach (var bar in _bars)
        {
            Visualizer.Items.Add(bar);
        }

        foreach (var dot in _processingDots)
        {
            ProcessingDots.Items.Add(dot);
        }

        CompositionTarget.Rendering += OnRendering;
    }

    public void ShowBottomCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Bottom - Height - 24;
        var wasVisible = IsVisible && !_isHiding;
        CancelWindowAnimations();
        if (!wasVisible)
        {
            Opacity = 0;
            _shellScale.ScaleX = MiniRecorderVisualSpec.WindowEntranceStartScale;
            _shellScale.ScaleY = MiniRecorderVisualSpec.WindowEntranceStartScale;
            _shellTranslate.Y = MiniRecorderVisualSpec.WindowEntranceTranslateY;
        }

        Show();
        _isHiding = false;
        if (!wasVisible)
        {
            FadeElement(this, 1, MiniRecorderVisualSpec.WindowEntranceMilliseconds, EntranceEase());
            AnimateDouble(_shellScale, ScaleTransform.ScaleXProperty, 1, MiniRecorderVisualSpec.WindowEntranceMilliseconds, EntranceEase());
            AnimateDouble(_shellScale, ScaleTransform.ScaleYProperty, 1, MiniRecorderVisualSpec.WindowEntranceMilliseconds, EntranceEase());
            AnimateDouble(_shellTranslate, TranslateTransform.YProperty, 0, MiniRecorderVisualSpec.WindowEntranceMilliseconds, EntranceEase());
        }
    }

    public new void Hide()
    {
        if (!IsVisible)
        {
            return;
        }

        _isHiding = true;
        CancelWindowAnimations();
        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(MiniRecorderVisualSpec.WindowExitMilliseconds),
            EasingFunction = ExitEase()
        };
        fade.Completed += (_, _) =>
        {
            if (_isHiding)
            {
                base.Hide();
                _isHiding = false;
            }
        };
        BeginAnimation(OpacityProperty, fade);
        AnimateDouble(_shellScale, ScaleTransform.ScaleXProperty, MiniRecorderVisualSpec.WindowEntranceStartScale, MiniRecorderVisualSpec.WindowExitMilliseconds, ExitEase());
        AnimateDouble(_shellScale, ScaleTransform.ScaleYProperty, MiniRecorderVisualSpec.WindowEntranceStartScale, MiniRecorderVisualSpec.WindowExitMilliseconds, ExitEase());
        AnimateDouble(_shellTranslate, TranslateTransform.YProperty, MiniRecorderVisualSpec.WindowExitTranslateY, MiniRecorderVisualSpec.WindowExitMilliseconds, ExitEase());
    }

    private void CancelWindowAnimations()
    {
        BeginAnimation(OpacityProperty, null);
        _shellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _shellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _shellTranslate.BeginAnimation(TranslateTransform.YProperty, null);
    }

    public Task InvokeAsync(Action action)
    {
        return Dispatcher.InvokeAsync(action).Task;
    }

    public async Task InvokeAsync(Func<Task> action)
    {
        var operation = await Dispatcher.InvokeAsync(action);
        await operation;
    }

    protected override void OnClosed(EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        base.OnClosed(e);
    }

    private static WpfRectangle CreateBar()
    {
        return new WpfRectangle
        {
            Width = 3,
            Height = 4,
            RadiusX = 1.5,
            RadiusY = 1.5,
            Margin = new Thickness(1, 0, 1, 0),
            Fill = new SolidColorBrush(ToWpfColor(MiniRecorderVisualSpec.CalculateAudioBarColor(4)))
        };
    }

    private static WpfColor ToWpfColor(AudioBarColor color)
    {
        return WpfColor.FromArgb(230, color.Red, color.Green, color.Blue);
    }

    private static WpfRectangle CreateProcessingDot()
    {
        return new WpfRectangle
        {
            Width = 3,
            Height = 3,
            RadiusX = 1.5,
            RadiusY = 1.5,
            Margin = new Thickness(2, 0, 2, 0),
            Fill = new SolidColorBrush(ToWpfColor(MiniRecorderVisualSpec.CalculateAudioBarColor(20))),
            Opacity = 0.35,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRender).TotalMilliseconds < MiniRecorderVisualSpec.RenderFrameThrottleMilliseconds)
        {
            return;
        }

        _lastRender = now;
        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        UpdateModeVisibility();
        UpdateLiveTranscriptOverlay();

        var pulseBucket = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / MiniRecorderVisualSpec.TranscribingPulseMilliseconds);
        if (_viewModel.ShowProcessingIndicator && pulseBucket != _lastProcessingPulseBucket)
        {
            _lastProcessingPulseBucket = pulseBucket;
            _viewModel.AdvanceProcessingPulse();
        }

        UpdateProcessingDots();

        for (var index = 0; index < _bars.Length; index++)
        {
            var height = MiniRecorderVisualSpec.CalculateBarHeight(
                index,
                _bars.Length,
                _viewModel.AudioLevel,
                time,
                _viewModel.IsRecording);
            _bars[index].Height = height;
            if (_bars[index].Fill is SolidColorBrush brush)
            {
                brush.Color = ToWpfColor(MiniRecorderVisualSpec.CalculateAudioBarColor(height));
            }
        }
    }

    private void UpdateModeVisibility()
    {
        if (_lastShowProcessingIndicator != _viewModel.ShowProcessingIndicator)
        {
            _lastShowProcessingIndicator = _viewModel.ShowProcessingIndicator;
            FadeElement(ProcessingDots, _viewModel.ShowProcessingIndicator ? 1 : 0);
        }

        var showVisualizer = !_viewModel.ShowProcessingIndicator && !_viewModel.HasStatusText;
        if (Visualizer.Visibility != WpfVisibility.Visible)
        {
            Visualizer.Visibility = WpfVisibility.Visible;
        }

        if (_lastShowVisualizer != showVisualizer)
        {
            _lastShowVisualizer = showVisualizer;
            FadeElement(Visualizer, showVisualizer ? 1 : 0);
        }

        if (_lastHasStatusText != _viewModel.HasStatusText)
        {
            _lastHasStatusText = _viewModel.HasStatusText;
            FadeElement(StatusTextBlock, _viewModel.HasStatusText ? 1 : 0);
        }
    }

    private void UpdateLiveTranscriptOverlay()
    {
        if (_lastHasLiveTranscript == _viewModel.HasLiveTranscript)
        {
            return;
        }

        _lastHasLiveTranscript = _viewModel.HasLiveTranscript;
        FadeElement(
            LiveTranscriptOverlay,
            _viewModel.HasLiveTranscript ? 1 : 0,
            MiniRecorderVisualSpec.PreviewOverlayFadeMilliseconds,
            StateEase());
        AnimateDouble(
            _previewTranslate,
            TranslateTransform.YProperty,
            _viewModel.HasLiveTranscript ? 0 : 4,
            MiniRecorderVisualSpec.PreviewOverlayFadeMilliseconds,
            StateEase());
    }

    private void UpdateProcessingDots()
    {
        for (var index = 0; index < _processingDots.Length; index++)
        {
            _processingDots[index].Opacity = MiniRecorderVisualSpec.CalculateProcessingDotOpacity(
                index,
                _viewModel.ProcessingPulseStep);
            var scale = MiniRecorderVisualSpec.CalculateProcessingDotScale(index, _viewModel.ProcessingPulseStep);
            _processingDotScales[index].ScaleX = scale;
            _processingDotScales[index].ScaleY = scale;
        }
    }

    private static void FadeElement(
        UIElement element,
        double opacity,
        int? milliseconds = null,
        IEasingFunction? easing = null)
    {
        if (Math.Abs(element.Opacity - opacity) < 0.01)
        {
            return;
        }

        element.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(milliseconds ?? MiniRecorderVisualSpec.StateFadeMilliseconds),
            EasingFunction = easing ?? StateEase()
        });
    }

    private static void AnimateDouble(
        Animatable target,
        DependencyProperty property,
        double value,
        int milliseconds,
        IEasingFunction? easing = null)
    {
        target.BeginAnimation(property, new DoubleAnimation
        {
            To = value,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = easing ?? StateEase()
        });
    }

    private static void AnimateDouble(
        FrameworkElement target,
        DependencyProperty property,
        double value,
        int milliseconds,
        IEasingFunction? easing = null)
    {
        var currentValue = target.GetValue(property);
        var from = currentValue is double currentDouble
            ? MiniRecorderVisualSpec.ResolveAnimationOrigin(currentDouble, value)
            : value;

        target.SetValue(property, from);
        target.BeginAnimation(property, new DoubleAnimation
        {
            From = from,
            To = value,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = easing ?? StateEase()
        });
    }

    private static void AnimateCornerRadius(Border border, CornerRadius radius, int milliseconds)
    {
        border.BeginAnimation(Border.CornerRadiusProperty, new CornerRadiusAnimation
        {
            To = radius,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = StateEase()
        });
    }

    private static IEasingFunction EntranceEase()
    {
        return new CubicEase { EasingMode = EasingMode.EaseOut };
    }

    private static IEasingFunction ExitEase()
    {
        return new CubicEase { EasingMode = EasingMode.EaseIn };
    }

    private static IEasingFunction StateEase()
    {
        return new CubicEase { EasingMode = EasingMode.EaseInOut };
    }

    private void RecorderShell_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void RecorderShell_OnMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.RequestSettings();
        e.Handled = true;
    }
}
