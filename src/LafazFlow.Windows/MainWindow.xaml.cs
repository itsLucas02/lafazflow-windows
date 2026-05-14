using System.Windows;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows;

public partial class MainWindow : Window
{
    private readonly MiniRecorderViewModel _miniRecorderViewModel = new();
    private readonly MiniRecorderWindow _miniRecorderWindow;

    public MainWindow()
    {
        InitializeComponent();
        _miniRecorderWindow = new MiniRecorderWindow(_miniRecorderViewModel);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
        _miniRecorderViewModel.State = RecordingState.Idle;
        _miniRecorderWindow.ShowBottomCenter();
    }
}
