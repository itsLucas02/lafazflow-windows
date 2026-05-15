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
    private string _statusBaseText = "";
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
            SetStatusBaseText(value switch
            {
                RecordingState.Transcribing => "Transcribing",
                RecordingState.Enhancing => "Enhancing",
                RecordingState.Error => "Error",
                _ => ""
            });
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsProcessing));
            OnPropertyChanged(nameof(HasStatusText));
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
        if (!IsProcessing || string.IsNullOrWhiteSpace(_statusBaseText))
        {
            return;
        }

        _processingPulseStep = (_processingPulseStep + 1) % 4;
        StatusText = _statusBaseText + new string('.', _processingPulseStep);
    }

    public void SetError(string message)
    {
        _state = RecordingState.Error;
        _statusBaseText = message;
        StatusText = message;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsProcessing));
        OnPropertyChanged(nameof(HasStatusText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetStatusBaseText(string statusText)
    {
        _statusBaseText = statusText;
        StatusText = statusText;
    }
}
