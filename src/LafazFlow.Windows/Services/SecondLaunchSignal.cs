namespace LafazFlow.Windows.Services;

public sealed class SecondLaunchSignal : IDisposable
{
    private readonly EventWaitHandle _signal;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _listenTask;

    private SecondLaunchSignal(string signalName, Action onSignal)
    {
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, signalName);
        _listenTask = Task.Run(() => Listen(onSignal));
    }

    public static SecondLaunchSignal Listen(string signalName, Action onSignal)
    {
        return new SecondLaunchSignal(signalName, onSignal);
    }

    public static void Signal(string signalName)
    {
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, signalName);
        signal.Set();
    }

    private void Listen(Action onSignal)
    {
        while (!_cancellation.IsCancellationRequested)
        {
            _signal.WaitOne();
            if (_cancellation.IsCancellationRequested)
            {
                return;
            }

            onSignal();
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _signal.Set();
        try
        {
            _listenTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _signal.Dispose();
        _cancellation.Dispose();
    }
}
