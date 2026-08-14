using System.Diagnostics;
using System.IO;
using System.Text;
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
    private readonly WhisperWorkerSupervisor? _workerSupervisor;
    private readonly ITranscriptionEngine? _workerEngine;
    private readonly PerformanceHealthMonitor _healthMonitor = new();
    private readonly VoiceEngineStatusSource _voiceEngineStatus;
    private SettingsWindow? _settingsWindow;
    private bool _shellInitialized;

    public MainWindow()
    {
        InitializeComponent();
        _voiceEngineStatus = new VoiceEngineStatusSource(_healthMonitor);
        var whisperProcesses = WhisperProcessCoordinator.Shared;
        _hotkeyService = new DoubleShiftHotkeyService(_hotkeyDiagnostics);
        _miniRecorderWindow = new MiniRecorderWindow(_miniRecorderViewModel);
        var transcriptionService = new WhisperCliTranscriptionService(whisperProcesses);
        var workerExecutable = ResolveWorkerExecutable();
        if (workerExecutable is not null)
        {
            _workerSupervisor = new WhisperWorkerSupervisor(new WhisperWorkerSupervisorOptions
            {
                WorkerExecutablePath = workerExecutable
            });
            _voiceEngineStatus.AttachSupervisor(_workerSupervisor);
            _workerEngine = new RecoveringTranscriptionEngine(
                new WorkerTranscriptionEngine(_workerSupervisor),
                new CliTranscriptionEngine(transcriptionService, transcriptionService),
                (settings, reason, cancellationToken) =>
                    _workerSupervisor.RestartSessionAsync(
                        settings,
                        cancellationToken,
                        string.IsNullOrWhiteSpace(reason) ? "Worker failure recovery" : reason));
        }

        var previewService = _workerSupervisor is null
            ? new RollingWhisperLiveTranscriptPreviewService(
                new RollingWhisperLiveTranscriptPreviewOptions(),
                hotkeyDiagnostics: _hotkeyDiagnostics,
                processCoordinator: whisperProcesses)
            : new RollingWhisperLiveTranscriptPreviewService(
                new RollingWhisperLiveTranscriptPreviewOptions(),
                hotkeyDiagnostics: _hotkeyDiagnostics,
                processCoordinator: whisperProcesses,
                workerTranscribeSnapshotAsync: WorkerPreviewTranscribeAsync);

        _recorderController = new RecorderController(
            _miniRecorderViewModel,
            _miniRecorderWindow,
            _audioCaptureService,
            transcriptionService,
            new ClipboardPasteService(),
            _settingsStore,
            livePreview: previewService,
            hotkeyDiagnostics: _hotkeyDiagnostics,
            transcriptionTiming: transcriptionService,
            transcriptionEngine: _workerEngine,
            performanceHealthMonitor: _healthMonitor,
            restartWorkerAsync: _workerSupervisor is null
                ? null
                : (settings, cancellationToken) =>
                    _workerSupervisor.RestartSessionAsync(
                        settings,
                        cancellationToken,
                        "Sustained slowdown"));
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
        if (_workerSupervisor is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _workerSupervisor.GetReadySessionAsync(
                        _settingsStore.Load(),
                        CancellationToken.None);
                    await Dispatcher.InvokeAsync(() => _trayIcon.ShowStartupNotification());
                }
                catch (Exception error)
                {
                    LogWorkerStartupFailure(error);
                    await Dispatcher.InvokeAsync(() => _trayIcon.ShowStartupNotification(
                        "LafazFlow is running, but the voice engine needs attention. Open Settings to check."));
                }
            }, CancellationToken.None);
        }
        else
        {
            _trayIcon.ShowStartupNotification();
        }
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
        if (_workerSupervisor is not null)
        {
            var supervisor = _workerSupervisor;
            var shutdownTask = Task.Run(() => supervisor.ShutdownAsync(), CancellationToken.None);
            shutdownTask.Wait(TimeSpan.FromSeconds(8));
            supervisor.Dispose();
        }

        _settingsWindow?.Close();
        _miniRecorderWindow.Close();
    }

    private static string? ResolveWorkerExecutable()
    {
        var appLocal = Path.Combine(AppContext.BaseDirectory, "lafazflow-whisper-worker.exe");
        if (File.Exists(appLocal))
        {
            return appLocal;
        }

        var configured = new WhisperWorkerSupervisorOptions().WorkerExecutablePath;
        return File.Exists(configured) ? configured : null;
    }

    private async Task<string?> WorkerPreviewTranscribeAsync(
        AppSettings settings,
        byte[] pcmAudio,
        uint sampleCount,
        CancellationToken cancellationToken)
    {
        if (_workerSupervisor is null)
        {
            return null;
        }

        try
        {
            var session = await _workerSupervisor.GetReadySessionAsync(settings, cancellationToken);
            await session.GetBackendAsync(cancellationToken);
            if (!WhisperBackendPolicy.IsWorkerCompatible(
                    settings,
                    session.CompiledBackend,
                    session.RuntimeBackend))
            {
                return null;
            }

            var response = await session.TranscribePreviewAsync(pcmAudio, sampleCount, cancellationToken);
            return response.Status == WhisperPipeStatus.Ok
                ? WhisperCliTranscriptionService.CleanTranscript(Encoding.UTF8.GetString(response.Data))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void LogWorkerStartupFailure(Exception? error)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LafazFlow",
                "Logs");
            BoundedLogFileWriter.AppendLine(
                Path.Combine(logRoot, "lafazflow.log"),
                $"[{DateTimeOffset.Now:O}] WORKER startup failed: {error?.Message}");
        }
        catch
        {
        }
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

            _settingsWindow = new SettingsWindow(SettingsViewModel.Load(
                _settingsStore,
                voiceEngineStatus: _voiceEngineStatus));
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }
}
