using System.ComponentModel;
using System.Runtime.CompilerServices;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.UI;

public sealed class MiniRecorderViewModel : INotifyPropertyChanged
{
    private RecordingState _state = RecordingState.Idle;
    private double _audioLevel;
    private string _statusText = "";

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
            StatusText = value switch
            {
                RecordingState.Transcribing => "Transcribing",
                RecordingState.Enhancing => "Enhancing",
                RecordingState.Error => "Error",
                _ => ""
            };
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

    public void SetError(string message)
    {
        _state = RecordingState.Error;
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
}
