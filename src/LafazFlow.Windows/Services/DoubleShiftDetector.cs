namespace LafazFlow.Windows.Services;

public sealed class DoubleShiftDetector
{
    private readonly TimeSpan _window;
    private DateTimeOffset? _lastShiftUpAt;

    public DoubleShiftDetector(TimeSpan window)
    {
        _window = window;
    }

    public bool RegisterShiftUp(DateTimeOffset now)
    {
        if (_lastShiftUpAt is not null && now - _lastShiftUpAt <= _window)
        {
            _lastShiftUpAt = null;
            return true;
        }

        _lastShiftUpAt = now;
        return false;
    }
}
