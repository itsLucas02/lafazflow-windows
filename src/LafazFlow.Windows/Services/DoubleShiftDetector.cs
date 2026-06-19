namespace LafazFlow.Windows.Services;

public sealed record DoubleShiftDetectionResult(bool Triggered, string Reason);

public sealed class DoubleShiftDetector
{
    private static readonly TimeSpan DefaultStaleDownTimeout = TimeSpan.FromMilliseconds(1000);

    private readonly TimeSpan _window;
    private readonly TimeSpan _staleDownTimeout;
    private DateTimeOffset? _lastShiftDownAt;
    private DateTimeOffset? _currentShiftDownAt;
    private bool _isDown;

    public DoubleShiftDetector(TimeSpan window, TimeSpan? staleDownTimeout = null)
    {
        _window = window;
        _staleDownTimeout = staleDownTimeout ?? DefaultStaleDownTimeout;
    }

    public bool RegisterKeyDown(DateTimeOffset now, bool isRepeat)
    {
        return RegisterKeyDownWithReason(now, isRepeat).Triggered;
    }

    public DoubleShiftDetectionResult RegisterKeyDownWithReason(DateTimeOffset now, bool isRepeat)
    {
        if (_isDown && _currentShiftDownAt is not null && now - _currentShiftDownAt > _staleDownTimeout)
        {
            _isDown = false;
            _currentShiftDownAt = null;
        }

        if (isRepeat)
        {
            return new DoubleShiftDetectionResult(false, "repeat");
        }

        if (_isDown)
        {
            return new DoubleShiftDetectionResult(false, "already_down");
        }

        _isDown = true;
        _currentShiftDownAt = now;
        if (_lastShiftDownAt is not null && now - _lastShiftDownAt <= _window)
        {
            _lastShiftDownAt = null;
            return new DoubleShiftDetectionResult(true, "second_shift");
        }

        _lastShiftDownAt = now;
        return new DoubleShiftDetectionResult(false, "first_shift");
    }

    public void RegisterKeyUp()
    {
        _isDown = false;
        _currentShiftDownAt = null;
    }
}
