using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.UI;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _settingsStore;
    private readonly LatencyDiagnosticLogStore _latencyDiagnostics;
    private readonly RuntimeDiagnosticsService _runtimeDiagnostics;
    private readonly string? _logsFolderOverride;
    private AppSettings _sourceSettings;
    private string _whisperCliPath = "";
    private string _cudaWhisperCliPath = "";
    private string _modelPath = "";
    private string _qualityModelPath = "";
    private int _whisperThreads;
    private TranscriptionProfile _transcriptionProfile;
    private WhisperBackend _whisperBackend;
    private bool _enableVad;
    private string _vadModelPath = "";
    private bool _restoreClipboardAfterPaste;
    private int _clipboardRestoreDelayMs;
    private bool _appendTrailingSpace;
    private bool _showLiveTranscriptPreview;
    private string _customVocabularyTerms = "";
    private bool _enableVocabularyCorrections;
    private bool _enableSoundCues;
    private double _soundCueVolumePercent;
    private bool _keepRecordingsForDiagnostics;
    private string _validationMessage = "";
    private string _runtimeProfileStatus = "";
    private string _runtimeDiagnosticsMessage = "";
    private string _latencyDiagnosticsMessage = "";
    private string _latestLatencySummary = "";

    private SettingsViewModel(
        SettingsStore settingsStore,
        AppSettings settings,
        LatencyDiagnosticLogStore latencyDiagnostics,
        RuntimeDiagnosticsService runtimeDiagnostics,
        string? logsFolderOverride)
    {
        _settingsStore = settingsStore;
        _latencyDiagnostics = latencyDiagnostics;
        _runtimeDiagnostics = runtimeDiagnostics;
        _logsFolderOverride = logsFolderOverride;
        _sourceSettings = settings;
        WhisperCliPath = settings.WhisperCliPath;
        CudaWhisperCliPath = settings.CudaWhisperCliPath;
        ModelPath = settings.ModelPath;
        QualityModelPath = settings.QualityModelPath;
        WhisperThreads = settings.WhisperThreads;
        TranscriptionProfile = settings.TranscriptionProfile;
        WhisperBackend = settings.WhisperBackend;
        EnableVad = settings.EnableVad;
        VadModelPath = settings.VadModelPath;
        RestoreClipboardAfterPaste = settings.RestoreClipboardAfterPaste;
        ClipboardRestoreDelayMs = settings.ClipboardRestoreDelayMs;
        AppendTrailingSpace = settings.AppendTrailingSpace;
        ShowLiveTranscriptPreview = settings.ShowLiveTranscriptPreview;
        CustomVocabularyTerms = settings.CustomVocabularyTerms;
        EnableVocabularyCorrections = settings.EnableVocabularyCorrections;
        EnableSoundCues = settings.EnableSoundCues;
        SoundCueVolumePercent = settings.SoundCueVolume * 100;
        KeepRecordingsForDiagnostics = settings.KeepRecordingsForDiagnostics;
        RefreshLatencyDiagnostics();
        RefreshRuntimeDiagnostics();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WhisperCliPath
    {
        get => _whisperCliPath;
        set => SetProperty(ref _whisperCliPath, value);
    }

    public string CudaWhisperCliPath
    {
        get => _cudaWhisperCliPath;
        set => SetProperty(ref _cudaWhisperCliPath, value);
    }

    public string ModelPath
    {
        get => _modelPath;
        set => SetProperty(ref _modelPath, value);
    }

    public string QualityModelPath
    {
        get => _qualityModelPath;
        set => SetProperty(ref _qualityModelPath, value);
    }

    public int WhisperThreads
    {
        get => _whisperThreads;
        set => SetProperty(ref _whisperThreads, value);
    }

    public TranscriptionProfile TranscriptionProfile
    {
        get => _transcriptionProfile;
        set => SetProperty(ref _transcriptionProfile, value);
    }

    public WhisperBackend WhisperBackend
    {
        get => _whisperBackend;
        set => SetProperty(ref _whisperBackend, value);
    }

    public bool EnableVad
    {
        get => _enableVad;
        set => SetProperty(ref _enableVad, value);
    }

    public string VadModelPath
    {
        get => _vadModelPath;
        set => SetProperty(ref _vadModelPath, value);
    }

    public IReadOnlyList<TranscriptionProfile> TranscriptionProfiles { get; } =
        Enum.GetValues<TranscriptionProfile>();

    public IReadOnlyList<WhisperBackend> WhisperBackends { get; } =
        Enum.GetValues<WhisperBackend>();

    public bool RestoreClipboardAfterPaste
    {
        get => _restoreClipboardAfterPaste;
        set => SetProperty(ref _restoreClipboardAfterPaste, value);
    }

    public int ClipboardRestoreDelayMs
    {
        get => _clipboardRestoreDelayMs;
        set => SetProperty(ref _clipboardRestoreDelayMs, value);
    }

    public bool AppendTrailingSpace
    {
        get => _appendTrailingSpace;
        set => SetProperty(ref _appendTrailingSpace, value);
    }

    public bool ShowLiveTranscriptPreview
    {
        get => _showLiveTranscriptPreview;
        set => SetProperty(ref _showLiveTranscriptPreview, value);
    }

    public string CustomVocabularyTerms
    {
        get => _customVocabularyTerms;
        set => SetProperty(ref _customVocabularyTerms, value);
    }

    public bool EnableVocabularyCorrections
    {
        get => _enableVocabularyCorrections;
        set => SetProperty(ref _enableVocabularyCorrections, value);
    }

    public bool EnableSoundCues
    {
        get => _enableSoundCues;
        set => SetProperty(ref _enableSoundCues, value);
    }

    public double SoundCueVolumePercent
    {
        get => _soundCueVolumePercent;
        set => SetProperty(ref _soundCueVolumePercent, value);
    }

    public bool KeepRecordingsForDiagnostics
    {
        get => _keepRecordingsForDiagnostics;
        set => SetProperty(ref _keepRecordingsForDiagnostics, value);
    }

    public ObservableCollection<LatencyDiagnosticRow> RecentLatencyRows { get; } = [];

    public ObservableCollection<RuntimeDiagnosticRow> RuntimeDiagnosticRows { get; } = [];

    public string RuntimeProfileStatus
    {
        get => _runtimeProfileStatus;
        private set => SetProperty(ref _runtimeProfileStatus, value);
    }

    public string RuntimeDiagnosticsMessage
    {
        get => _runtimeDiagnosticsMessage;
        private set => SetProperty(ref _runtimeDiagnosticsMessage, value);
    }

    public string LatencyDiagnosticsMessage
    {
        get => _latencyDiagnosticsMessage;
        private set => SetProperty(ref _latencyDiagnosticsMessage, value);
    }

    public string LatestLatencySummary
    {
        get => _latestLatencySummary;
        private set => SetProperty(ref _latestLatencySummary, value);
    }

    public string SettingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LafazFlow");

    public string LogsFolder => _logsFolderOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LafazFlow",
        "Logs");

    public string RecordingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LafazFlow",
        "Recordings");

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage == value)
            {
                return;
            }

            _validationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public static SettingsViewModel Load(
        SettingsStore settingsStore,
        LatencyDiagnosticLogStore? latencyDiagnostics = null,
        RuntimeDiagnosticsService? runtimeDiagnostics = null,
        string? logsFolderOverride = null)
    {
        return new SettingsViewModel(
            settingsStore,
            settingsStore.Load(),
            latencyDiagnostics ?? new LatencyDiagnosticLogStore(),
            runtimeDiagnostics ?? new RuntimeDiagnosticsService(),
            logsFolderOverride);
    }

    public void RefreshRuntimeDiagnostics()
    {
        RuntimeDiagnosticRows.Clear();
        var settings = BuildEditedSettings();
        RuntimeProfileStatus = _runtimeDiagnostics.BuildProfileStatus(settings);
        foreach (var row in _runtimeDiagnostics.BuildDiagnostics(settings, LogsFolder))
        {
            RuntimeDiagnosticRows.Add(row);
        }

        var errorCount = RuntimeDiagnosticRows.Count(row => row.Severity == RuntimeDiagnosticSeverity.Error);
        RuntimeDiagnosticsMessage = errorCount == 0
            ? "Runtime ready."
            : $"Runtime has {errorCount} issue(s).";
    }

    public void TestMicrophone()
    {
        ReplaceRuntimeDiagnostic(_runtimeDiagnostics.TestMicrophone());
        RuntimeDiagnosticsMessage = RuntimeDiagnosticRows
            .First(row => row.Name == "Microphone")
            .Detail;
    }

    public async Task TestTranscriptionSmokeAsync(CancellationToken cancellationToken)
    {
        var row = await _runtimeDiagnostics.TestTranscriptionSmokeAsync(BuildEditedSettings(), cancellationToken);
        ReplaceRuntimeDiagnostic(row);
        RuntimeDiagnosticsMessage = row.Detail;
    }

    public void ResetSettingsToDefaults()
    {
        ApplySettings(_settingsStore.ResetToDefaults());
        ValidationMessage = "";
        RefreshRuntimeDiagnostics();
        RuntimeDiagnosticsMessage = "Settings reset to detected defaults.";
    }

    public void RefreshLatencyDiagnostics()
    {
        RecentLatencyRows.Clear();
        foreach (var row in _latencyDiagnostics.LoadRecent())
        {
            RecentLatencyRows.Add(row);
        }

        LatencyDiagnosticsMessage = RecentLatencyRows.Count == 0
            ? "No latency entries yet."
            : $"Showing latest {RecentLatencyRows.Count} latency entries.";
        LatestLatencySummary = RecentLatencyRows.Count == 0
            ? "No latency summary yet."
            : BuildLatestLatencySummary(RecentLatencyRows[0]);
    }

    public void ClearLatencyDiagnostics()
    {
        var removed = _latencyDiagnostics.ClearLatencyLines();
        RefreshLatencyDiagnostics();
        LatencyDiagnosticsMessage = removed == 0
            ? "No latency entries to clear."
            : $"Cleared {removed} latency entries.";
    }

    private static string BuildLatestLatencySummary(LatencyDiagnosticRow row)
    {
        return row.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            ? $"Latest failed: total {row.TotalStopToDoneMs} ms, whisper {row.WhisperMs} ms, paste {row.PasteMs} ms, queue {row.QueueWaitMs} ms, hotkey {row.HotkeyToVisibleMs} ms, error {row.Error}."
            : $"Latest: total {row.TotalStopToDoneMs} ms, whisper {row.WhisperMs} ms, paste {row.PasteMs} ms, queue {row.QueueWaitMs} ms, hotkey {row.HotkeyToVisibleMs} ms.";
    }

    public SettingsSaveResult Save()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors);
            return SettingsSaveResult.Failed(errors);
        }

        var settings = BuildEditedSettings();

        _settingsStore.Save(settings);
        ApplySettings(settings);
        ValidationMessage = "";
        RefreshRuntimeDiagnostics();
        return SettingsSaveResult.Ok;
    }

    private AppSettings BuildEditedSettings()
    {
        return _sourceSettings with
        {
            WhisperCliPath = WhisperCliPath.Trim(),
            CudaWhisperCliPath = CudaWhisperCliPath.Trim(),
            ModelPath = ModelPath.Trim(),
            QualityModelPath = QualityModelPath.Trim(),
            WhisperThreads = Math.Clamp(WhisperThreads, 1, Environment.ProcessorCount),
            TranscriptionProfile = TranscriptionProfile,
            WhisperBackend = WhisperBackend,
            EnableVad = EnableVad,
            VadModelPath = VadModelPath.Trim(),
            RestoreClipboardAfterPaste = RestoreClipboardAfterPaste,
            ClipboardRestoreDelayMs = ClipboardRestoreDelayMs <= 250
                ? AppSettings.DefaultClipboardRestoreDelayMs
                : ClipboardRestoreDelayMs,
            AppendTrailingSpace = AppendTrailingSpace,
            ShowLiveTranscriptPreview = ShowLiveTranscriptPreview,
            CustomVocabularyTerms = CustomVocabularyTerms,
            EnableVocabularyCorrections = EnableVocabularyCorrections,
            EnableSoundCues = EnableSoundCues,
            SoundCueVolume = Math.Clamp(SoundCueVolumePercent / 100.0, 0, 1),
            KeepRecordingsForDiagnostics = KeepRecordingsForDiagnostics
        };
    }

    private void ApplySettings(AppSettings settings)
    {
        _sourceSettings = settings;
        WhisperCliPath = settings.WhisperCliPath;
        CudaWhisperCliPath = settings.CudaWhisperCliPath;
        ModelPath = settings.ModelPath;
        QualityModelPath = settings.QualityModelPath;
        WhisperThreads = settings.WhisperThreads;
        TranscriptionProfile = settings.TranscriptionProfile;
        WhisperBackend = settings.WhisperBackend;
        EnableVad = settings.EnableVad;
        VadModelPath = settings.VadModelPath;
        RestoreClipboardAfterPaste = settings.RestoreClipboardAfterPaste;
        ClipboardRestoreDelayMs = settings.ClipboardRestoreDelayMs;
        AppendTrailingSpace = settings.AppendTrailingSpace;
        ShowLiveTranscriptPreview = settings.ShowLiveTranscriptPreview;
        CustomVocabularyTerms = settings.CustomVocabularyTerms;
        EnableVocabularyCorrections = settings.EnableVocabularyCorrections;
        EnableSoundCues = settings.EnableSoundCues;
        SoundCueVolumePercent = settings.SoundCueVolume * 100;
        KeepRecordingsForDiagnostics = settings.KeepRecordingsForDiagnostics;
    }

    private void ReplaceRuntimeDiagnostic(RuntimeDiagnosticRow row)
    {
        var existingIndex = RuntimeDiagnosticRows
            .Select((value, index) => new { value, index })
            .FirstOrDefault(item => item.value.Name == row.Name)
            ?.index;
        if (existingIndex is null)
        {
            RuntimeDiagnosticRows.Add(row);
            return;
        }

        RuntimeDiagnosticRows[existingIndex.Value] = row;
    }

    private List<string> Validate()
    {
        var errors = new List<string>();
        if (!File.Exists(WhisperCliPath.Trim()))
        {
            errors.Add("Whisper CLI path does not exist.");
        }

        if (!File.Exists(ModelPath.Trim()))
        {
            errors.Add("Model path does not exist.");
        }

        if (TranscriptionProfile == TranscriptionProfile.Quality
            && !File.Exists(QualityModelPath.Trim()))
        {
            errors.Add("Quality model path does not exist.");
        }

        if (TranscriptionProfile == TranscriptionProfile.Quality
            && WhisperBackend == WhisperBackend.Cuda
            && !File.Exists(CudaWhisperCliPath.Trim()))
        {
            errors.Add("CUDA Whisper CLI path does not exist.");
        }

        if (EnableVad && !File.Exists(VadModelPath.Trim()))
        {
            errors.Add("VAD model path does not exist.");
        }

        return errors;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}
