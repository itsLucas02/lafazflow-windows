using System.Diagnostics;

namespace LafazFlow.Windows.Services;

public sealed class LatencyTrace
{
    private readonly Func<long> _getTimestamp;
    private readonly long _timestampFrequency;
    private readonly Dictionary<LatencyCheckpoint, long> _checkpoints = [];
    private readonly object _gate = new();

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
        Mark(checkpoint, _getTimestamp());
    }

    public void Mark(LatencyCheckpoint checkpoint, long timestamp)
    {
        lock (_gate)
        {
            _checkpoints[checkpoint] = timestamp;
        }
    }

    public bool HasCheckpoint(LatencyCheckpoint checkpoint)
    {
        lock (_gate)
        {
            return _checkpoints.ContainsKey(checkpoint);
        }
    }

    public long? ElapsedMilliseconds(LatencyCheckpoint start, LatencyCheckpoint end)
    {
        long startTimestamp;
        long endTimestamp;
        lock (_gate)
        {
            if (!_checkpoints.TryGetValue(start, out startTimestamp)
                || !_checkpoints.TryGetValue(end, out endTimestamp))
            {
                return null;
            }
        }

        if (endTimestamp < startTimestamp)
        {
            return null;
        }

        var elapsedTicks = endTimestamp - startTimestamp;
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
