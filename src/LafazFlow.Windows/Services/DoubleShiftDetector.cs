namespace LafazFlow.Windows.Services;

public sealed class DoubleShiftDetector
{
    private readonly TimeSpan _window;
    private DateTimeOffset? _lastShiftDownAt;
    private bool _isDown;

    public DoubleShiftDetector(TimeSpan window)
    {
        _window = window;
    }

    public bool RegisterKeyDown(DateTimeOffset now, bool isRepeat)
    {
        if (isRepeat || _isDown)
        {
            return false;
        }

        _isDown = true;
        if (_lastShiftDownAt is not null && now - _lastShiftDownAt <= _window)
        {
            _lastShiftDownAt = null;
            return true;
        }

        _lastShiftDownAt = now;
        return false;
    }

    public void RegisterKeyUp()
    {
        _isDown = false;
    }
}
