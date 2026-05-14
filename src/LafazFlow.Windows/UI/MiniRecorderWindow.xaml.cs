using System.Windows;
using System.Windows.Media;
using WpfVisibility = System.Windows.Visibility;
using WpfColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace LafazFlow.Windows.UI;

public partial class MiniRecorderWindow : Window
{
    private readonly MiniRecorderViewModel _viewModel;
    private readonly WpfRectangle[] _bars;
    private readonly double[] _phases;
    private DateTime _lastRender = DateTime.UtcNow;

    public MiniRecorderWindow(MiniRecorderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _bars = Enumerable.Range(0, 15).Select(_ => CreateBar()).ToArray();
        _phases = Enumerable.Range(0, 15).Select(index => index * 0.4).ToArray();

        foreach (var bar in _bars)
        {
            Visualizer.Items.Add(bar);
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

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRender).TotalMilliseconds < 16)
        {
            return;
        }

        _lastRender = now;
        var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var amplitude = Math.Pow(_viewModel.AudioLevel, 0.7);
        Visualizer.Visibility = _viewModel.HasStatusText ? WpfVisibility.Hidden : WpfVisibility.Visible;

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

    private void RecorderShell_OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}
