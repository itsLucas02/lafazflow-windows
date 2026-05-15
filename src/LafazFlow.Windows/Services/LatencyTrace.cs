using System.Diagnostics;

namespace LafazFlow.Windows.Services;

public sealed class LatencyTrace
{
    private readonly Func<long> _getTimestamp;
    private readonly long _timestampFrequency;
    private readonly Dictionary<LatencyCheckpoint, long> _checkpoints = [];

    public LatencyTrace()
        : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
    {
    }

    public LatencyTrace(Func<long> getTimestamp, long timestampFrequency)
    {
        _getTimestamp = getTimestamp;
        _timestampFrequency = timestampFrequency;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    public LatencyStatus Status { get; private set; } = LatencyStatus.Running;

    public string ModelPath { get; init; } = "";

    public int Threads { get; init; }

    public string? TargetProcessName { get; init; }

    public string ErrorKind { get; private set; } = "";

    public void Mark(LatencyCheckpoint checkpoint)
    {
        _checkpoints[checkpoint] = _getTimestamp();
    }

    public bool HasCheckpoint(LatencyCheckpoint checkpoint)
    {
        return _checkpoints.ContainsKey(checkpoint);
    }

    public long? ElapsedMilliseconds(LatencyCheckpoint start, LatencyCheckpoint end)
    {
        if (!_checkpoints.TryGetValue(start, out var startTimestamp)
            || !_checkpoints.TryGetValue(end, out var endTimestamp))
        {
            return null;
        }

        var elapsedTicks = Math.Max(0, endTimestamp - startTimestamp);
        return elapsedTicks * 1000 / _timestampFrequency;
    }

    public void Complete()
    {
        Status = LatencyStatus.Completed;
        Mark(LatencyCheckpoint.Completed);
    }

    public void Fail(Exception error)
    {
        Fail(error.GetType().Name);
    }

    public void Fail(string errorKind)
    {
        Status = LatencyStatus.Failed;
        ErrorKind = string.IsNullOrWhiteSpace(errorKind) ? "Exception" : errorKind;
        Mark(LatencyCheckpoint.Failed);
    }
}
