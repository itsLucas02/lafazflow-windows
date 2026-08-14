using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class VoiceEngineStatusSourceTests
{
    [Fact]
    public void NoWorkerReportsCompatibilityEngine()
    {
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());

        var snapshot = source.Snapshot("ABC123");

        Assert.Equal("Using compatibility engine", snapshot.StatusText);
        Assert.Equal("Unknown", snapshot.ActiveBackendText);
        Assert.Equal("Not ready yet", snapshot.UptimeText);
        Assert.Equal("No recovery yet", snapshot.LastRecoveryText);
        Assert.Equal("Engine ABC123", snapshot.EngineIdText);
    }

    [Fact]
    public void ActiveBackendReportsCliCompatibilityWhenNoWorker()
    {
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        var settings = AppSettings.Default;

        var snapshot = source.Snapshot("ABC123", settings);

        Assert.Equal(
            $"CLI compatibility ({WhisperBackendPolicy.RequiredBackend(settings)})",
            snapshot.ActiveBackendText);
    }

    [Fact]
    public async Task ActiveBackendReportsPersistentWorkerWhenCompatible()
    {
        var factory = new FakeWorkerProcessFactory(
            () => { },
            compiledBackend: "cuda",
            runtimeBackend: "cuda");
        using var supervisor = CreateSupervisor(factory);
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        source.AttachSupervisor(supervisor);
        var settings = CudaSettings;

        var session = await supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        await session.GetBackendAsync(CancellationToken.None);

        var snapshot = source.Snapshot(EngineSettingsFingerprint.Compute(settings), settings);

        Assert.Equal("Persistent worker (Cuda)", snapshot.ActiveBackendText);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task ActiveBackendReportsCliCompatibilityOnBackendMismatch()
    {
        var factory = new FakeWorkerProcessFactory(
            () => { },
            compiledBackend: "cpu",
            runtimeBackend: "cpu");
        using var supervisor = CreateSupervisor(factory);
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        source.AttachSupervisor(supervisor);
        var settings = CudaSettings;

        var session = await supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        await session.GetBackendAsync(CancellationToken.None);

        var snapshot = source.Snapshot(EngineSettingsFingerprint.Compute(settings), settings);

        Assert.Equal(
            "CLI compatibility (Cuda) — worker backend mismatch",
            snapshot.ActiveBackendText);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task ReadyWorkerReportsReadyAndUptime()
    {
        var now = DateTimeOffset.UtcNow;
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var monitor = new PerformanceHealthMonitor();
        var source = new VoiceEngineStatusSource(monitor, () => now);
        source.AttachSupervisor(supervisor);

        var session = await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);

        Assert.True(session.IsReady);
        var snapshot = source.Snapshot(EngineSettingsFingerprint.Compute(AppSettings.Default));
        Assert.Equal("Ready", snapshot.StatusText);
        Assert.Equal("Under 1 minute", snapshot.UptimeText);

        now = now.AddMinutes(5);
        snapshot = source.Snapshot(EngineSettingsFingerprint.Compute(AppSettings.Default));
        Assert.Equal("5 minutes", snapshot.UptimeText);

        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task UnavailableWorkerReportsAttention()
    {
        var factory = new FakeWorkerProcessFactory(() => { }, respondToInitialize: false);
        using var supervisor = new WhisperWorkerSupervisor(
            new WhisperWorkerSupervisorOptions
            {
                ReadinessTimeout = TimeSpan.FromMilliseconds(200),
                OperationTimeout = TimeSpan.FromSeconds(5)
            },
            () => factory.Create());
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        source.AttachSupervisor(supervisor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None));

        Assert.Equal(
            "Voice engine needs attention",
            source.Snapshot(EngineSettingsFingerprint.Compute(AppSettings.Default)).StatusText);
    }

    [Fact]
    public async Task RecoveryRecordsReasonAndOutcome()
    {
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        source.AttachSupervisor(supervisor);

        await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        await supervisor.RestartSessionAsync(AppSettings.Default, CancellationToken.None, "Sustained slowdown");

        var snapshot = source.Snapshot(EngineSettingsFingerprint.Compute(AppSettings.Default));
        Assert.Contains("Sustained slowdown", snapshot.LastRecoveryText);
        Assert.Contains("worker restarted and is ready", snapshot.LastRecoveryText);
        Assert.Equal("Ready", snapshot.StatusText);

        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task FailedRecoveryRecordsOutcome()
    {
        var factory = new FakeWorkerProcessFactory(() => { }, respondToInitialize: false);
        using var supervisor = new WhisperWorkerSupervisor(
            new WhisperWorkerSupervisorOptions
            {
                ReadinessTimeout = TimeSpan.FromMilliseconds(200),
                OperationTimeout = TimeSpan.FromSeconds(5)
            },
            () => factory.Create());
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        source.AttachSupervisor(supervisor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.RestartSessionAsync(AppSettings.Default, CancellationToken.None, "Worker timeout"));

        var snapshot = source.Snapshot(EngineSettingsFingerprint.Compute(AppSettings.Default));
        Assert.Contains("Worker timeout", snapshot.LastRecoveryText);
        Assert.Contains("restart failed", snapshot.LastRecoveryText);
    }

    [Fact]
    public async Task RetriedSuccessReportsUsingRecoveryEngine()
    {
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var monitor = new PerformanceHealthMonitor();
        var source = new VoiceEngineStatusSource(monitor);
        source.AttachSupervisor(supervisor);

        await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        var fingerprint = EngineSettingsFingerprint.Compute(AppSettings.Default);
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300, 10000));
        }

        monitor.Record(Sample(fingerprint, 300, 10000, retried: true));

        Assert.Equal("Using recovery engine", source.Snapshot(fingerprint).StatusText);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task DisconnectedWorkerReportsRecovering()
    {
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var source = new VoiceEngineStatusSource(new PerformanceHealthMonitor());
        source.AttachSupervisor(supervisor);

        var session = await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        factory.LatestProcess?.Disconnect();
        await WaitUntilAsync(() => !session.IsReady);

        Assert.Equal(
            "Recovering voice engine",
            source.Snapshot(EngineSettingsFingerprint.Compute(AppSettings.Default)).StatusText);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task LatencySummariesReportColdAndWarmMedianP95()
    {
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var monitor = new PerformanceHealthMonitor();
        var source = new VoiceEngineStatusSource(monitor);
        source.AttachSupervisor(supervisor);

        await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        var fingerprint = EngineSettingsFingerprint.Compute(AppSettings.Default);
        monitor.Record(Sample(fingerprint, 1400, 10000, cold: true));
        for (var i = 0; i < 10; i++)
        {
            monitor.Record(Sample(fingerprint, 300 + (i * 10), 10000));
        }

        var snapshot = source.Snapshot(fingerprint);
        Assert.Contains("1.4 s median", snapshot.ColdLatencyText);
        Assert.Contains("350 ms median", snapshot.WarmLatencyText);
        Assert.Contains("p95", snapshot.WarmLatencyText);
        await supervisor.ShutdownAsync();
    }

    private static WhisperWorkerSupervisor CreateSupervisor(FakeWorkerProcessFactory factory)
    {
        return new WhisperWorkerSupervisor(
            new WhisperWorkerSupervisorOptions
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

    private static HealthSample Sample(
        string fingerprint,
        long inferenceMs,
        long audioDurationMs,
        bool cold = false,
        bool retried = false)
    {
        return new HealthSample(
            Guid.NewGuid(),
            fingerprint,
            inferenceMs,
            audioDurationMs,
            cold,
            retried,
            false,
            false,
            DateTimeOffset.UtcNow);
    }

    private static AppSettings CudaSettings => AppSettings.Default with
    {
        TranscriptionProfile = TranscriptionProfile.Quality,
        WhisperBackend = WhisperBackend.Cuda,
        EnableVad = true,
        WhisperThreads = 16
    };
}
