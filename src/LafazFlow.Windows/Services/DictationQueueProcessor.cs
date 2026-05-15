namespace LafazFlow.Windows.Services;

public sealed class DictationQueueProcessor
{
    private readonly Func<DictationJob, CancellationToken, Task> _processJobAsync;
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;
    private int _pendingCount;

    public DictationQueueProcessor(Func<DictationJob, CancellationToken, Task> processJobAsync)
    {
        _processJobAsync = processJobAsync;
    }

    public event Action<int>? PendingCountChanged;

    public int PendingCount => Volatile.Read(ref _pendingCount);

    public Task Enqueue(DictationJob job, CancellationToken cancellationToken = default)
    {
        var pending = Interlocked.Increment(ref _pendingCount);
        PendingCountChanged?.Invoke(pending);

        lock (_gate)
        {
            _tail = _tail.ContinueWith(
                    _ => ProcessSafelyAsync(job, cancellationToken),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();
            return _tail;
        }
    }

    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            return _tail;
        }
    }

    private async Task ProcessSafelyAsync(DictationJob job, CancellationToken cancellationToken)
    {
        try
        {
            await _processJobAsync(job, cancellationToken);
        }
        catch
        {
        }
        finally
        {
            var pending = Interlocked.Decrement(ref _pendingCount);
            PendingCountChanged?.Invoke(pending);
        }
    }
}
