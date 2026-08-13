using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperWorkerSupervisorTests
{
    [Fact]
    public async Task TwoConcurrentStartupCallsProduceOneWorker()
    {
        var started = 0;
        var factory = new FakeWorkerProcessFactory(() => Interlocked.Increment(ref started));
        using var supervisor = CreateSupervisor(factory);
        var settings = AppSettings.Default;

        var first = supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        var second = supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        var sessions = await Task.WhenAll(first, second);

        Assert.Same(sessions[0], sessions[1]);
        Assert.Equal(1, started);
        Assert.True(sessions[0].IsReady);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task FingerprintChangeStartsReplacementWorker()
    {
        var started = 0;
        var factory = new FakeWorkerProcessFactory(() => Interlocked.Increment(ref started));
        using var supervisor = CreateSupervisor(factory);

        var first = await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        var second = await supervisor.GetReadySessionAsync(
            AppSettings.Default with { WhisperThreads = 8 },
            CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, started);
        Assert.NotEqual(first.FingerprintHex, second.FingerprintHex);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task WorkerExitMovesStateToRecovering()
    {
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var states = new List<WhisperWorkerState>();
        supervisor.StateChanged += states.Add;

        var session = await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        factory.LatestProcess?.Disconnect();
        await WaitUntilAsync(() => supervisor.State == WhisperWorkerState.Recovering);

        Assert.Contains(WhisperWorkerState.Recovering, states);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task ReadinessTimeoutMovesStateToUnavailable()
    {
        var factory = new FakeWorkerProcessFactory(() => { }, respondToInitialize: false);
        var options = new WhisperWorkerSupervisorOptions
        {
            ReadinessTimeout = TimeSpan.FromMilliseconds(200),
            OperationTimeout = TimeSpan.FromSeconds(5)
        };
        using var supervisor = CreateSupervisor(factory, options);
        var states = new List<WhisperWorkerState>();
        supervisor.StateChanged += states.Add;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None));

        Assert.Contains(WhisperWorkerState.Unavailable, states);
    }

    private static WhisperWorkerSupervisor CreateSupervisor(
        FakeWorkerProcessFactory factory,
        WhisperWorkerSupervisorOptions? options = null)
    {
        return new WhisperWorkerSupervisor(
            options ?? new WhisperWorkerSupervisorOptions
            {
                ReadinessTimeout = TimeSpan.FromSeconds(60),
                OperationTimeout = TimeSpan.FromSeconds(10),
                ShutdownTimeout = TimeSpan.FromSeconds(2)
            },
            () => factory.Create());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Condition was not met in time.");
    }
}
