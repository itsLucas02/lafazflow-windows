using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class RecoveringTranscriptionEngineTests
{
    [Fact]
    public async Task PrimarySuccessDoesNotRestartOrFallback()
    {
        var restarts = 0;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(Success("primary")),
            fallback: new StubEngine(Success("fallback"), calls: () => fallbackCalls++),
            restart: () => restarts++);

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("primary", result.Text);
        Assert.Equal(0, restarts);
        Assert.Equal(0, fallbackCalls);
    }

    [Fact]
    public async Task RetryableFailureRestartsWorkerAndRetriesBeforeCliFallback()
    {
        var restarts = 0;
        var primaryCalls = 0;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(() =>
            {
                primaryCalls++;
                return primaryCalls == 1 ? Failure("worker_unavailable") : Success("retried");
            }),
            fallback: new StubEngine(Success("fallback"), calls: () => fallbackCalls++),
            restart: () => restarts++);

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("retried", result.Text);
        Assert.Equal(1, restarts);
        Assert.Equal(0, fallbackCalls);
    }

    [Fact]
    public async Task WorkerRetryFailureFallsBackToCli()
    {
        var restarts = 0;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(Failure("worker_timeout")),
            fallback: new StubEngine(Success("cli"), calls: () => fallbackCalls++),
            restart: () => restarts++);

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("cli", result.Text);
        Assert.Equal(1, restarts);
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public async Task TerminalFailurePastesNothingAndPreservesFailure()
    {
        var restarts = 0;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(Failure("worker_timeout")),
            fallback: new StubEngine(Failure("cli_failed"), calls: () => fallbackCalls++),
            restart: () => restarts++);

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(1, restarts);
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public async Task NonRetryableFailureSkipsRecovery()
    {
        var restarts = 0;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(Failure("invalid_audio")),
            fallback: new StubEngine(Success("fallback"), calls: () => fallbackCalls++),
            restart: () => restarts++);

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, restarts);
        Assert.Equal(0, fallbackCalls);
    }

    [Fact]
    public async Task RestartReceivesFailureReasonForRecoveryDiagnostics()
    {
        string? restartReason = null;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(Failure("pipe_broken")),
            fallback: new StubEngine(Success("cli"), calls: () => fallbackCalls++),
            restart: reason =>
            {
                restartReason = reason;
                return Task.CompletedTask;
            });

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("pipe_broken", restartReason);
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public async Task BackendMismatchSkipsWorkerRestartAndUsesCliFallback()
    {
        var restarts = 0;
        var fallbackCalls = 0;
        var engine = CreateEngine(
            primary: new StubEngine(Failure("worker_backend_mismatch")),
            fallback: new StubEngine(Success("cli"), calls: () => fallbackCalls++),
            restart: () => restarts++);

        var result = await engine.TranscribeAsync("a.wav", AppSettings.Default, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("cli", result.Text);
        Assert.True(result.WasRetried);
        Assert.Equal(0, restarts);
        Assert.Equal(1, fallbackCalls);
    }

    private static RecoveringTranscriptionEngine CreateEngine(
        ITranscriptionEngine primary,
        ITranscriptionEngine fallback,
        Action restart)
    {
        return new RecoveringTranscriptionEngine(
            primary,
            fallback,
            (_, _, _) =>
            {
                restart();
                return Task.CompletedTask;
            });
    }

    private static RecoveringTranscriptionEngine CreateEngine(
        ITranscriptionEngine primary,
        ITranscriptionEngine fallback,
        Func<string, Task> restart)
    {
        return new RecoveringTranscriptionEngine(
            primary,
            fallback,
            (_, reason, _) => restart(reason));
    }

    private static TranscriptionEngineResult Success(string text)
    {
        return new TranscriptionEngineResult(text, true, null, null, null);
    }

    private static TranscriptionEngineResult Failure(string kind)
    {
        return new TranscriptionEngineResult("", false, kind, null, null);
    }

    private sealed class StubEngine : ITranscriptionEngine
    {
        private readonly Func<TranscriptionEngineResult> _result;
        private readonly Action? _calls;

        public StubEngine(TranscriptionEngineResult result, Action? calls = null)
        {
            _result = () => result;
            _calls = calls;
        }

        public StubEngine(Func<TranscriptionEngineResult> result, Action? calls = null)
        {
            _result = result;
            _calls = calls;
        }

        public Task<TranscriptionEngineResult> TranscribeAsync(
            string audioPath,
            AppSettings settings,
            Guid dictationId,
            CancellationToken cancellationToken)
        {
            _calls?.Invoke();
            return Task.FromResult(_result());
        }
    }
}
