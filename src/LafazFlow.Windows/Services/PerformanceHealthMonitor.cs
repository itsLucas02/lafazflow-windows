namespace LafazFlow.Windows.Services;

public sealed record HealthSample(
    Guid DictationId,
    string FingerprintHex,
    long InferenceMs,
    long AudioDurationMs,
    bool IsCold,
    bool IsRetried,
    bool IsCancelled,
    bool IsFailed,
    DateTimeOffset Timestamp);

public sealed class PerformanceHealthMonitorOptions
{
    public int BaselineSampleCount { get; init; } = 10;

    public int WindowSize { get; init; } = 30;

    public long SlowInferenceDeltaMs { get; init; } = 750;

    public double SlowRtfFactor { get; init; } = 1.75;

    public int DegradationWindow { get; init; } = 5;

    public int DegradationThreshold { get; init; } = 3;

    public TimeSpan RestartCooldown { get; init; } = TimeSpan.FromMinutes(10);

    public long MinimumEligibleAudioMs { get; init; } = 2000;
}

public sealed class PerformanceHealthMonitor
{
    private readonly PerformanceHealthMonitorOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, FingerprintWindow> _windows = [];

    public PerformanceHealthMonitor(
        PerformanceHealthMonitorOptions? options = null,
        Func<DateTimeOffset>? clock = null)
    {
        _options = options ?? new PerformanceHealthMonitorOptions();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public event Action<string>? SustainedDegradation;

    public IReadOnlyCollection<HealthSample> RecentSamples(string fingerprintHex)
    {
        return _windows.TryGetValue(fingerprintHex, out var window)
            ? window.EligibleSamples.ToArray()
            : [];
    }

    public bool IsEligible(HealthSample sample)
    {
        return !sample.IsCold
            && !sample.IsRetried
            && !sample.IsCancelled
            && !sample.IsFailed
            && sample.AudioDurationMs >= _options.MinimumEligibleAudioMs
            && sample.InferenceMs > 0;
    }

    public bool IsSlow(string fingerprintHex, HealthSample sample)
    {
        if (!_windows.TryGetValue(fingerprintHex, out var window) || !window.HasBaseline)
        {
            return false;
        }

        var inferenceAbove = sample.InferenceMs >= window.BaselineMedianMs + _options.SlowInferenceDeltaMs;
        var rtfAbove = sample.AudioDurationMs > 0
            && (double)sample.InferenceMs / sample.AudioDurationMs
                >= window.BaselineMedianRtf * _options.SlowRtfFactor;
        return inferenceAbove && rtfAbove;
    }

    public bool Record(HealthSample sample)
    {
        if (!IsEligible(sample))
        {
            return false;
        }

        var window = GetOrCreateWindow(sample.FingerprintHex);
        window.Add(sample);
        if (!window.HasBaseline || !IsSlow(sample.FingerprintHex, sample))
        {
            return false;
        }

        if (window.SlowInRecent(windowSize: _options.DegradationWindow)
                >= _options.DegradationThreshold
            && (_options.RestartCooldown <= TimeSpan.Zero
                || window.LastRestartUtc is null
                || _clock() - window.LastRestartUtc >= _options.RestartCooldown))
        {
            window.LastRestartUtc = _clock();
            window.MarkNextSampleAsCold = true;
            SustainedDegradation?.Invoke(sample.FingerprintHex);
            return true;
        }

        return false;
    }

    private FingerprintWindow GetOrCreateWindow(string fingerprintHex)
    {
        if (!_windows.TryGetValue(fingerprintHex, out var window))
        {
            window = new FingerprintWindow(_options);
            _windows[fingerprintHex] = window;
        }

        return window;
    }

    private sealed class FingerprintWindow
    {
        private readonly PerformanceHealthMonitorOptions _options;
        private readonly List<HealthSample> _samples = [];

        public FingerprintWindow(PerformanceHealthMonitorOptions options)
        {
            _options = options;
        }

        public bool HasBaseline => EligibleSamples.Count >= _options.BaselineSampleCount;

        public DateTimeOffset? LastRestartUtc { get; set; }

        public bool MarkNextSampleAsCold { get; set; }

        public long BaselineMedianMs => MedianMs(EligibleSamples);

        public double BaselineMedianRtf => MedianRtf(EligibleSamples);

        public IReadOnlyList<HealthSample> EligibleSamples => _samples
            .Where(sample => !sample.IsCold && !sample.IsRetried && !sample.IsCancelled && !sample.IsFailed)
            .ToList();

        public void Add(HealthSample sample)
        {
            var effectiveSample = MarkNextSampleAsCold
                ? sample with { IsCold = true }
                : sample;
            MarkNextSampleAsCold = false;
            _samples.Add(effectiveSample);
            if (_samples.Count > _options.WindowSize)
            {
                _samples.RemoveRange(0, _samples.Count - _options.WindowSize);
            }
        }

        public int SlowInRecent(int windowSize)
        {
            var eligible = EligibleSamples;
            var recent = eligible.Skip(Math.Max(0, eligible.Count - windowSize)).ToArray();
            var slow = 0;
            foreach (var sample in recent)
            {
                if (!HasBaseline)
                {
                    continue;
                }

                var inferenceAbove = sample.InferenceMs >= BaselineMedianMs + _options.SlowInferenceDeltaMs;
                var rtfAbove = sample.AudioDurationMs > 0
                    && (double)sample.InferenceMs / sample.AudioDurationMs
                        >= BaselineMedianRtf * _options.SlowRtfFactor;
                if (inferenceAbove && rtfAbove)
                {
                    slow++;
                }
            }

            return slow;
        }

        private static long MedianMs(IEnumerable<HealthSample> samples)
        {
            var ordered = samples.Select(sample => sample.InferenceMs).OrderBy(value => value).ToArray();
            return ordered.Length == 0 ? 0 : ordered[ordered.Length / 2];
        }

        private static double MedianRtf(IEnumerable<HealthSample> samples)
        {
            var values = samples
                .Where(sample => sample.AudioDurationMs > 0)
                .Select(sample => (double)sample.InferenceMs / sample.AudioDurationMs)
                .OrderBy(value => value)
                .ToArray();
            return values.Length == 0 ? 0 : values[values.Length / 2];
        }
    }
}
