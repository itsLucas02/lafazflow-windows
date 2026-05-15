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
    private DateTime _lastRender = DateTime.UtcNow;
    private bool _lastShowVisualizer = true;
    private bool _lastShowProcessingIndicator;
    private bool _lastHasStatusText;
    private bool _lastHasLiveTranscript;
    private int _lastProcessingPulseBucket = -1;

    public MiniRecorderWindow(MiniRecorderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _bars = Enumerable.Range(0, 15).Select(_ => CreateBar()).ToArray();
        _processingDots = Enumerable.Range(0, 5).Select(_ => CreateProcessingDot()).ToArray();

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
        var wasVisible = IsVisible;
        if (!wasVisible)
        {
            Opacity = 0;
        }

        Show();
        if (!wasVisible)
        {
            FadeElement(this, 1);
        }
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
            Fill = new SolidColorBrush(WpfColor.FromArgb(128, 255, 255, 255))
        };
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
            Fill = new SolidColorBrush(WpfColor.FromArgb(230, 255, 255, 255)),
            Opacity = 0.35
        };
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRender).TotalMilliseconds < 16)
        {
            return;
        }

        _lastRender = now;
        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        UpdateModeVisibility();
        UpdateLiveTranscriptLayout();

        var pulseBucket = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / MiniRecorderVisualSpec.TranscribingPulseMilliseconds);
        if (_viewModel.ShowProcessingIndicator && pulseBucket != _lastProcessingPulseBucket)
        {
            _lastProcessingPulseBucket = pulseBucket;
            _viewModel.AdvanceProcessingPulse();
        }

        UpdateProcessingDots();

        for (var index = 0; index < _bars.Length; index++)
        {
            _bars[index].Height = MiniRecorderVisualSpec.CalculateBarHeight(
                index,
                _bars.Length,
                _viewModel.AudioLevel,
                time,
                _viewModel.IsRecording);
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

    private void UpdateLiveTranscriptLayout()
    {
        if (_lastHasLiveTranscript == _viewModel.HasLiveTranscript)
        {
            return;
        }

        _lastHasLiveTranscript = _viewModel.HasLiveTranscript;
        var targetWidth = _viewModel.HasLiveTranscript
            ? MiniRecorderVisualSpec.ExpandedWidth
            : MiniRecorderVisualSpec.CompactWidth;
        var targetHeight = _viewModel.HasLiveTranscript
            ? MiniRecorderVisualSpec.ControlBarHeight + MiniRecorderVisualSpec.LiveTranscriptPanelHeight
            : MiniRecorderVisualSpec.ControlBarHeight;
        var targetRadius = _viewModel.HasLiveTranscript
            ? MiniRecorderVisualSpec.ExpandedCornerRadius
            : MiniRecorderVisualSpec.CompactCornerRadius;

        AnimateDouble(RecorderShell, WidthProperty, targetWidth, MiniRecorderVisualSpec.ExpansionMilliseconds);
        AnimateDouble(RecorderShell, HeightProperty, targetHeight, MiniRecorderVisualSpec.ExpansionMilliseconds);
        AnimateCornerRadius(RecorderShell, new CornerRadius(targetRadius), MiniRecorderVisualSpec.ExpansionMilliseconds);
        TranscriptRow.BeginAnimation(RowDefinition.HeightProperty, new GridLengthAnimation
        {
            To = new GridLength(_viewModel.HasLiveTranscript ? MiniRecorderVisualSpec.LiveTranscriptPanelHeight : 0),
            Duration = TimeSpan.FromMilliseconds(MiniRecorderVisualSpec.ExpansionMilliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
        FadeElement(LiveTranscriptPanel, _viewModel.HasLiveTranscript ? 1 : 0, MiniRecorderVisualSpec.ExpansionMilliseconds);
    }

    private void UpdateProcessingDots()
    {
        for (var index = 0; index < _processingDots.Length; index++)
        {
            var distance = Math.Abs(index - _viewModel.ProcessingPulseStep);
            _processingDots[index].Opacity = distance == 0 ? 0.85 : 0.25;
        }
    }

    private static void FadeElement(UIElement element, double opacity, int? milliseconds = null)
    {
        if (Math.Abs(element.Opacity - opacity) < 0.01)
        {
            return;
        }

        element.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(milliseconds ?? MiniRecorderVisualSpec.StateFadeMilliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private static void AnimateDouble(FrameworkElement target, DependencyProperty property, double value, int milliseconds)
    {
        target.BeginAnimation(property, new DoubleAnimation
        {
            To = value,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private static void AnimateCornerRadius(Border border, CornerRadius radius, int milliseconds)
    {
        border.BeginAnimation(Border.CornerRadiusProperty, new CornerRadiusAnimation
        {
            To = radius,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private void RecorderShell_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}
