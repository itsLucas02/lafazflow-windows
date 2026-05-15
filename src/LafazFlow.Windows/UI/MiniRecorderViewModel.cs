using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.UI;

public sealed class MiniRecorderViewModel : INotifyPropertyChanged
{
    private const double AudioSmoothingPreviousWeight = 0.6;
    private const double AudioSmoothingNextWeight = 0.4;

    private RecordingState _state = RecordingState.Idle;
    private double _audioLevel;
    private bool _hasAudioSample;
    private string _partialTranscript = "";
    private string _statusText = "";
    private int _processingPulseStep;
    private int _pendingTranscriptionCount;

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
            if (value != RecordingState.Recording)
            {
                PartialTranscript = "";
                _audioLevel = 0;
                _hasAudioSample = false;
            }

            StatusText = value == RecordingState.Error ? "Error" : "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(AudioLevel));
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsProcessing));
            OnPropertyChanged(nameof(ShowProcessingIndicator));
            OnPropertyChanged(nameof(HasStatusText));
            OnPropertyChanged(nameof(ProcessingPulseStep));
            OnPropertyChanged(nameof(HasLiveTranscript));
        }
    }

    public double AudioLevel
    {
        get => _audioLevel;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            var smoothed = _hasAudioSample
                ? _audioLevel * AudioSmoothingPreviousWeight + clamped * AudioSmoothingNextWeight
                : clamped;

            if (Math.Abs(_audioLevel - smoothed) < double.Epsilon)
            {
                return;
            }

            _hasAudioSample = true;
            _audioLevel = smoothed;
            OnPropertyChanged();
        }
    }

    public string PartialTranscript
    {
        get => _partialTranscript;
        set
        {
            var next = value.Trim();
            if (_partialTranscript == next)
            {
                return;
            }

            _partialTranscript = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLiveTranscript));
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

    public bool ShowProcessingIndicator => IsProcessing || (HasPendingTranscriptions && !IsRecording);

    public int ProcessingPulseStep => _processingPulseStep;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public bool HasLiveTranscript => IsRecording && !string.IsNullOrWhiteSpace(PartialTranscript);

    public int PendingTranscriptionCount
    {
        get => _pendingTranscriptionCount;
        set
        {
            var clamped = Math.Max(0, value);
            if (_pendingTranscriptionCount == clamped)
            {
                return;
            }

            _pendingTranscriptionCount = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPendingTranscriptions));
            OnPropertyChanged(nameof(ShowProcessingIndicator));
        }
    }

    public bool HasPendingTranscriptions => PendingTranscriptionCount > 0;

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
        if (!ShowProcessingIndicator)
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
