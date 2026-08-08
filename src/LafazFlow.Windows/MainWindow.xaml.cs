using System.Diagnostics;
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
    private readonly FileHotkeyDiagnostics _hotkeyDiagnostics = new();
    private readonly DoubleShiftHotkeyService _hotkeyService;
    private readonly SettingsStore _settingsStore = new();
    private readonly RecorderController _recorderController;
    private readonly TrayIconService _trayIcon;
    private SettingsWindow? _settingsWindow;
    private bool _shellInitialized;

    public MainWindow()
    {
        InitializeComponent();
        var whisperProcesses = WhisperProcessCoordinator.Shared;
        _hotkeyService = new DoubleShiftHotkeyService(_hotkeyDiagnostics);
        _miniRecorderWindow = new MiniRecorderWindow(_miniRecorderViewModel);
        _recorderController = new RecorderController(
            _miniRecorderViewModel,
            _miniRecorderWindow,
            _audioCaptureService,
            new WhisperCliTranscriptionService(whisperProcesses),
            new ClipboardPasteService(),
            _settingsStore,
            livePreview: new RollingWhisperLiveTranscriptPreviewService(
                new RollingWhisperLiveTranscriptPreviewOptions(),
                hotkeyDiagnostics: _hotkeyDiagnostics,
                processCoordinator: whisperProcesses),
            hotkeyDiagnostics: _hotkeyDiagnostics);
        _trayIcon = new TrayIconService(
            _miniRecorderViewModel,
            ShowSettingsFromShell,
            TrayIconService.OpenLogsFolder,
            () => System.Windows.Application.Current.Shutdown());
        _miniRecorderViewModel.SettingsRequested += OnSettingsRequested;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public bool IsFirstRun => _settingsStore.IsFirstRun;

    public void MarkOnboardingComplete()
    {
        _settingsStore.MarkOnboardingComplete();
    }

    public void InitializeShell()
    {
        if (_shellInitialized)
        {
            return;
        }

        _shellInitialized = true;
        Hide();
        _miniRecorderViewModel.State = RecordingState.Idle;
        _hotkeyService.DoubleShiftPressed += OnDoubleShiftPressed;
        _hotkeyService.Start();
        _trayIcon.ShowStartupNotification();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeShell();
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

    private void OnDoubleShiftPressed(long hotkeyTimestamp)
    {
        _ = Dispatcher.BeginInvoke(async () =>
        {
            var dispatchMs = ElapsedMilliseconds(hotkeyTimestamp, Stopwatch.GetTimestamp());
            _hotkeyDiagnostics.Log(new HotkeyDiagnosticWrite(
                Event: "dispatched",
                Accepted: "true",
                DispatchMs: dispatchMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Reason: "begin_invoke"));
            await _recorderController.ToggleAsync(hotkeyTimestamp);
        });
    }

    private static long ElapsedMilliseconds(long startTimestamp, long endTimestamp)
    {
        if (endTimestamp < startTimestamp)
        {
            return 0;
        }

        return (endTimestamp - startTimestamp) * 1000 / Stopwatch.Frequency;
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
