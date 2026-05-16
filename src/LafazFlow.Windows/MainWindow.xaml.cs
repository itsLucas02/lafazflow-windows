using System.Windows;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows;

public partial class MainWindow : Window
{
    private readonly MiniRecorderViewModel _miniRecorderViewModel = new();
    private readonly MiniRecorderWindow _miniRecorderWindow;
    private readonly AudioCaptureService _audioCaptureService = new();
    private readonly DoubleShiftHotkeyService _hotkeyService = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly RecorderController _recorderController;
    private readonly TrayIconService _trayIcon;
    private SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();
        _miniRecorderWindow = new MiniRecorderWindow(_miniRecorderViewModel);
        _recorderController = new RecorderController(
            _miniRecorderViewModel,
            _miniRecorderWindow,
            _audioCaptureService,
            new WhisperCliTranscriptionService(),
            new ClipboardPasteService(),
            _settingsStore,
            livePreview: new RollingWhisperLiveTranscriptPreviewService());
        _trayIcon = new TrayIconService(
            _miniRecorderViewModel,
            ShowSettingsFromShell,
            TrayIconService.OpenLogsFolder,
            () => System.Windows.Application.Current.Shutdown());
        _miniRecorderViewModel.SettingsRequested += OnSettingsRequested;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Hide();
        _miniRecorderViewModel.State = RecordingState.Idle;
        _hotkeyService.DoubleShiftPressed += OnDoubleShiftPressed;
        _hotkeyService.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeyService.DoubleShiftPressed -= OnDoubleShiftPressed;
        _miniRecorderViewModel.SettingsRequested -= OnSettingsRequested;
        _trayIcon.Dispose();
        _hotkeyService.Dispose();
        _audioCaptureService.Dispose();
        _settingsWindow?.Close();
        _miniRecorderWindow.Close();
    }

    private void OnDoubleShiftPressed()
    {
        _ = Dispatcher.BeginInvoke(async () => await _recorderController.ToggleAsync());
    }

    private void OnSettingsRequested()
    {
        ShowSettingsFromShell();
    }

    public void ShowSettingsFromShell()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow is { IsVisible: true })
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(SettingsViewModel.Load(_settingsStore));
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }
}
