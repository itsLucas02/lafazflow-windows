using System.Windows;
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
    private readonly double[] _phases;
    private DateTime _lastRender = DateTime.UtcNow;

    public MiniRecorderWindow(MiniRecorderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _bars = Enumerable.Range(0, 15).Select(_ => CreateBar()).ToArray();
        _processingDots = Enumerable.Range(0, 5).Select(_ => CreateProcessingDot()).ToArray();
        _phases = Enumerable.Range(0, 15).Select(index => index * 0.4).ToArray();

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
        Show();
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
            Fill = new SolidColorBrush(WpfColor.FromArgb(217, 255, 255, 255))
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

        var previousRender = _lastRender;
        _lastRender = now;
        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var amplitude = Math.Pow(_viewModel.AudioLevel, 0.7);
        Visualizer.Visibility = !_viewModel.ShowProcessingIndicator && !_viewModel.HasStatusText
            ? WpfVisibility.Visible
            : WpfVisibility.Hidden;
        ProcessingDots.Visibility = _viewModel.ShowProcessingIndicator
            ? WpfVisibility.Visible
            : WpfVisibility.Collapsed;
        StatusTextBlock.Visibility = _viewModel.HasStatusText
            ? WpfVisibility.Visible
            : WpfVisibility.Collapsed;

        if (_viewModel.ShowProcessingIndicator && (now.Millisecond / 300) != (previousRender.Millisecond / 300))
        {
            _viewModel.AdvanceProcessingPulse();
        }

        UpdateProcessingDots();

        for (var index = 0; index < _bars.Length; index++)
        {
            var wave = Math.Sin(time * 8 + _phases[index]) * 0.5 + 0.5;
            var centerDistance = Math.Abs(index - _bars.Length / 2.0) / (_bars.Length / 2.0);
            var centerBoost = 1.0 - centerDistance * 0.4;
            _bars[index].Height = _viewModel.IsRecording
                ? Math.Max(4, 4 + amplitude * wave * centerBoost * 24)
                : 4;
        }
    }

    private void UpdateProcessingDots()
    {
        for (var index = 0; index < _processingDots.Length; index++)
        {
            var distance = Math.Abs(index - _viewModel.ProcessingPulseStep);
            _processingDots[index].Opacity = distance == 0 ? 1.0 : distance == 1 ? 0.62 : 0.32;
        }
    }

    private void RecorderShell_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}
