using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed record RecoveryRecord(
    string Reason,
    bool Succeeded,
    DateTimeOffset OccurredAtUtc);

public sealed record VoiceEngineStatusSnapshot(
    string StatusText,
    string UptimeText,
    string ColdLatencyText,
    string WarmLatencyText,
    string LastRecoveryText,
    string EngineIdText);

public sealed class VoiceEngineStatusSource
{
    private readonly PerformanceHealthMonitor _monitor;
    private readonly Func<DateTimeOffset> _clock;
    private WhisperWorkerSupervisor? _supervisor;
    private DateTimeOffset? _readySinceUtc;
    private RecoveryRecord? _lastRecovery;

    public VoiceEngineStatusSource(
        PerformanceHealthMonitor monitor,
        Func<DateTimeOffset>? clock = null)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public event Action? Changed;

    public bool HasWorker => _supervisor is not null;

    public void AttachSupervisor(WhisperWorkerSupervisor? supervisor)
    {
        if (_supervisor is not null)
        {
            _supervisor.StateChanged -= OnStateChanged;
            _supervisor.RecoveryRecorded -= OnRecoveryRecorded;
        }

        _supervisor = supervisor;
        if (_supervisor is not null)
        {
            _supervisor.StateChanged += OnStateChanged;
            _supervisor.RecoveryRecorded += OnRecoveryRecorded;
        }

        _readySinceUtc = null;
        RaiseChanged();
    }

    public VoiceEngineStatusSnapshot Snapshot(string fingerprintHex)
    {
        var diagnostics = _monitor.DiagnosticSamples(fingerprintHex).ToList();
        return new VoiceEngineStatusSnapshot(
            StatusText(diagnostics),
            UptimeText(),
            LatencyText(diagnostics, cold: true),
            LatencyText(diagnostics, cold: false),
            RecoveryText(),
            EngineIdText(fingerprintHex));
    }

    private string StatusText(IReadOnlyList<HealthSample> diagnostics)
    {
        if (_supervisor is null)
        {
            return "Using compatibility engine";
        }

        if (_supervisor.State == WhisperWorkerState.Unavailable)
        {
            return "Voice engine needs attention";
        }

        if (_supervisor.State == WhisperWorkerState.Recovering)
        {
            return "Recovering voice engine";
        }

        if (_supervisor.State is WhisperWorkerState.Starting
            or WhisperWorkerState.Loading
            or WhisperWorkerState.Idle)
        {
            return "Loading voice engine";
        }

        var lastSuccess = diagnostics.LastOrDefault(sample => !sample.IsCancelled && !sample.IsFailed);
        return lastSuccess is { IsRetried: true }
            ? "Using recovery engine"
            : "Ready";
    }

    private string UptimeText()
    {
        if (_readySinceUtc is not { } readySince)
        {
            return "Not ready yet";
        }

        var elapsed = _clock() - readySince;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalMinutes < 1)
        {
            return "Under 1 minute";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes} minute{(elapsed.TotalMinutes >= 2 ? "s" : "")}";
        }

        return $"{(int)elapsed.TotalHours}h {(int)(elapsed.TotalMinutes % 60)}m";
    }

    private static string LatencyText(IReadOnlyList<HealthSample> samples, bool cold)
    {
        var eligible = samples
            .Where(sample => !sample.IsCancelled && !sample.IsFailed)
            .Where(sample => cold
                ? sample.IsCold
                : !sample.IsCold && !sample.IsRetried && sample.AudioDurationMs >= 2000)
            .Select(sample => sample.InferenceMs)
            .Where(inferenceMs => inferenceMs > 0)
            .OrderBy(inferenceMs => inferenceMs)
            .ToArray();
        if (eligible.Length == 0)
        {
            return "n/a";
        }

        var median = eligible[eligible.Length / 2];
        var p95 = Percentile(eligible, 0.95);
        return $"{FormatMilliseconds(median)} median · {FormatMilliseconds(p95)} p95 · {eligible.Length} runs";
    }

    private string RecoveryText()
    {
        if (_lastRecovery is not { } recovery)
        {
            return "No recovery yet";
        }

        var outcome = recovery.Succeeded
            ? "worker restarted and is ready"
            : "restart failed — check Diagnostics";
        return $"{recovery.OccurredAtUtc.ToLocalTime():HH:mm} — {recovery.Reason} · {outcome}";
    }

    private static string EngineIdText(string fingerprintHex)
    {
        var safeLength = Math.Min(12, fingerprintHex.Length);
        return $"Engine {fingerprintHex[..safeLength]}";
    }

    private void OnStateChanged(WhisperWorkerState state)
    {
        if (state == WhisperWorkerState.Ready)
        {
            _readySinceUtc = _clock();
        }

        RaiseChanged();
    }

    private void OnRecoveryRecorded(string reason, bool succeeded)
    {
        _lastRecovery = new RecoveryRecord(reason, succeeded, _clock());
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }

    private static long Percentile(IReadOnlyList<long> orderedValues, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * orderedValues.Count) - 1;
        return orderedValues[Math.Clamp(rank, 0, orderedValues.Count - 1)];
    }

    private static string FormatMilliseconds(long milliseconds)
    {
        return milliseconds < 1000
            ? $"{milliseconds} ms"
            : $"{milliseconds / 1000.0:0.0} s";
    }
}
