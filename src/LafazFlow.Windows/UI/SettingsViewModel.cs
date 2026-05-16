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
    private AppSettings _sourceSettings;
    private string _whisperCliPath = "";
    private string _modelPath = "";
    private int _whisperThreads;
    private bool _restoreClipboardAfterPaste;
    private int _clipboardRestoreDelayMs;
    private bool _appendTrailingSpace;
    private bool _showLiveTranscriptPreview;
    private bool _enableVocabularyCorrections;
    private bool _keepRecordingsForDiagnostics;
    private string _validationMessage = "";
    private string _latencyDiagnosticsMessage = "";

    private SettingsViewModel(
        SettingsStore settingsStore,
        AppSettings settings,
        LatencyDiagnosticLogStore latencyDiagnostics)
    {
        _settingsStore = settingsStore;
        _latencyDiagnostics = latencyDiagnostics;
        _sourceSettings = settings;
        WhisperCliPath = settings.WhisperCliPath;
        ModelPath = settings.ModelPath;
        WhisperThreads = settings.WhisperThreads;
        RestoreClipboardAfterPaste = settings.RestoreClipboardAfterPaste;
        ClipboardRestoreDelayMs = settings.ClipboardRestoreDelayMs;
        AppendTrailingSpace = settings.AppendTrailingSpace;
        ShowLiveTranscriptPreview = settings.ShowLiveTranscriptPreview;
        EnableVocabularyCorrections = settings.EnableVocabularyCorrections;
        KeepRecordingsForDiagnostics = settings.KeepRecordingsForDiagnostics;
        RefreshLatencyDiagnostics();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WhisperCliPath
    {
        get => _whisperCliPath;
        set => SetProperty(ref _whisperCliPath, value);
    }

    public string ModelPath
    {
        get => _modelPath;
        set => SetProperty(ref _modelPath, value);
    }

    public int WhisperThreads
    {
        get => _whisperThreads;
        set => SetProperty(ref _whisperThreads, value);
    }

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

    public bool EnableVocabularyCorrections
    {
        get => _enableVocabularyCorrections;
        set => SetProperty(ref _enableVocabularyCorrections, value);
    }

    public bool KeepRecordingsForDiagnostics
    {
        get => _keepRecordingsForDiagnostics;
        set => SetProperty(ref _keepRecordingsForDiagnostics, value);
    }

    public ObservableCollection<LatencyDiagnosticRow> RecentLatencyRows { get; } = [];

    public string LatencyDiagnosticsMessage
    {
        get => _latencyDiagnosticsMessage;
        private set => SetProperty(ref _latencyDiagnosticsMessage, value);
    }

    public string SettingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LafazFlow");

    public string LogsFolder => Path.Combine(
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

    public static SettingsViewModel Load(SettingsStore settingsStore, LatencyDiagnosticLogStore? latencyDiagnostics = null)
    {
        return new SettingsViewModel(
            settingsStore,
            settingsStore.Load(),
            latencyDiagnostics ?? new LatencyDiagnosticLogStore());
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
    }

    public void ClearLatencyDiagnostics()
    {
        var removed = _latencyDiagnostics.ClearLatencyLines();
        RefreshLatencyDiagnostics();
        LatencyDiagnosticsMessage = removed == 0
            ? "No latency entries to clear."
            : $"Cleared {removed} latency entries.";
    }

    public SettingsSaveResult Save()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors);
            return SettingsSaveResult.Failed(errors);
        }

        var settings = _sourceSettings with
        {
            WhisperCliPath = WhisperCliPath.Trim(),
            ModelPath = ModelPath.Trim(),
            WhisperThreads = Math.Clamp(WhisperThreads, 1, Environment.ProcessorCount),
            RestoreClipboardAfterPaste = RestoreClipboardAfterPaste,
            ClipboardRestoreDelayMs = ClipboardRestoreDelayMs <= 250
                ? AppSettings.DefaultClipboardRestoreDelayMs
                : ClipboardRestoreDelayMs,
            AppendTrailingSpace = AppendTrailingSpace,
            ShowLiveTranscriptPreview = ShowLiveTranscriptPreview,
            EnableVocabularyCorrections = EnableVocabularyCorrections,
            KeepRecordingsForDiagnostics = KeepRecordingsForDiagnostics
        };

        _settingsStore.Save(settings);
        _sourceSettings = settings;
        WhisperThreads = settings.WhisperThreads;
        ClipboardRestoreDelayMs = settings.ClipboardRestoreDelayMs;
        ValidationMessage = "";
        return SettingsSaveResult.Ok;
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
