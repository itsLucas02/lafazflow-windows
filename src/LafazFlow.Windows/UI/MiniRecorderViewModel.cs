using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.UI;

public sealed class MiniRecorderViewModel : INotifyPropertyChanged
{
    private RecordingState _state = RecordingState.Idle;
    private double _audioLevel;
    private string _statusText = "";
    private int _processingPulseStep;

    public event PropertyChangedEventHandler? PropertyChanged;

    public RecordingState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            _processingPulseStep = 0;
            StatusText = value == RecordingState.Error ? "Error" : "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsProcessing));
            OnPropertyChanged(nameof(ShowProcessingIndicator));
            OnPropertyChanged(nameof(HasStatusText));
            OnPropertyChanged(nameof(ProcessingPulseStep));
        }
    }

    public double AudioLevel
    {
        get => _audioLevel;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(_audioLevel - clamped) < double.Epsilon)
            {
                return;
            }

            _audioLevel = clamped;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusText));
        }
    }

    public bool IsRecording => State == RecordingState.Recording;

    public bool IsProcessing => State is RecordingState.Transcribing or RecordingState.Enhancing;

    public bool ShowProcessingIndicator => IsProcessing;

    public int ProcessingPulseStep => _processingPulseStep;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public ObservableCollection<string> RecentTranscripts { get; } = [];

    public bool HasRecentTranscripts => RecentTranscripts.Count > 0;

    public string LastTranscriptPreview => HasRecentTranscripts ? RecentTranscripts[0] : "";

    public void AddCompletedTranscript(string transcript)
    {
        var trimmed = transcript.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        RecentTranscripts.Insert(0, trimmed);
        while (RecentTranscripts.Count > 5)
        {
            RecentTranscripts.RemoveAt(RecentTranscripts.Count - 1);
        }

        OnPropertyChanged(nameof(HasRecentTranscripts));
        OnPropertyChanged(nameof(LastTranscriptPreview));
    }

    public void AdvanceProcessingPulse()
    {
        if (!IsProcessing)
        {
            return;
        }

        _processingPulseStep = (_processingPulseStep + 1) % 5;
        OnPropertyChanged(nameof(ProcessingPulseStep));
    }

    public void SetError(string message)
    {
        _state = RecordingState.Error;
        _processingPulseStep = 0;
        StatusText = message;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsProcessing));
        OnPropertyChanged(nameof(ShowProcessingIndicator));
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(ProcessingPulseStep));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
