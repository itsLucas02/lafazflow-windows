using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LafazFlow.Windows.Services;

public sealed class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private static readonly TimeSpan DefaultStopDeadline = TimeSpan.FromSeconds(2);
    private const int PreRollBytes = 16000;

    private readonly object _sessionLock = new();
    private readonly Func<int?, IAudioInputDevice> _createInputDevice;
    private readonly Func<string, WaveFormat, IAudioCaptureWriter> _createWriter;
    private readonly TimeSpan _stopDeadline;
    private CaptureSession? _activeSession;
    private CaptureSession? _stoppingSession;
    private IAudioInputDevice? _warmInput;
    private string _warmPreference = "";
    private string _warmDeviceDescription = "unknown";
    private bool _warmAvailable;
    private int _warmGeneration;
    private EventHandler<WaveInEventArgs>? _warmDataHandler;
    private EventHandler<StoppedEventArgs>? _warmStoppedHandler;
    private readonly Queue<byte> _preRoll = new(PreRollBytes);
    private TaskCompletionSource<bool> _warmAudioSource = NewAudioSource();
    private long _callbackSequence;
    private long _lastCallbackTimestamp;

    public AudioCaptureService()
        : this(
            _ => new WasapiAudioInputDevice(_ ?? -1),
            (path, format) => new WaveFileAudioCaptureWriter(path, format),
            null)
    {
    }

    internal AudioCaptureService(
        Func<int?, IAudioInputDevice> createInputDevice,
        Func<string, WaveFormat, IAudioCaptureWriter> createWriter,
        TimeSpan? stopDeadline = null)
    {
        _createInputDevice = createInputDevice;
        _createWriter = createWriter;
        _stopDeadline = stopDeadline ?? DefaultStopDeadline;
    }

    public event Action<double>? AudioLevelChanged;

    public event Action<byte[]>? AudioChunkAvailable;

    public bool HasReceivedAudio => _activeSession?.HasReceivedAudio ?? false;

    public string? ActiveInputDeviceName { get; private set; }

    public void WarmUp(string? preferredInputDeviceName = null)
    {
        lock (_sessionLock)
        {
            EnsureWarmInput(preferredInputDeviceName);
        }
    }

    public string Start(string outputDirectory, string? preferredInputDeviceName = null)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}.wav");
        CaptureSession session;
        lock (_sessionLock)
        {
            if (_activeSession is not null)
            {
                throw new InvalidOperationException("A microphone recording is already active.");
            }

            EnsureWarmInput(preferredInputDeviceName);
            var writer = _createWriter(outputPath, _warmInput!.WaveFormat);
            try
            {
                session = new CaptureSession(
                    writer, PublishAudioChunk, outputPath, [.. _preRoll],
                    _callbackSequence, _warmDeviceDescription, _warmInput.WaveFormat);
            }
            catch
            {
                writer.Dispose();
                throw;
            }

            _preRoll.Clear();
            _activeSession = session;
            if (_warmInput is WasapiAudioInputDevice wasapi)
            {
                try { wasapi.BeginNativeTrace(outputPath); }
                catch (Exception error) { LogCaptureFailure($"Native capture trace unavailable: {error.GetType().Name}"); }
            }
        }

        return outputPath;
    }

    public async Task<AudioCaptureFinalization> StopAsync()
    {
        CaptureSession? session;
        string preference;
        lock (_sessionLock)
        {
            session = _activeSession;
            _activeSession = null;
            _stoppingSession = session;
            preference = _warmPreference;
        }

        if (session is null)
        {
            throw new InvalidOperationException("No active microphone recording.");
        }

        try
        {
            _warmInput?.StopRecording();
        }
        catch (Exception error)
        {
            session.SignalStopped(error);
        }

        var finalization = await session.StopAsync(_stopDeadline);
        try
        {
            LogCaptureFailure(session.DiagnosticSummary(finalization));
        }
        catch
        {
            // Diagnostics must never prevent a finalized recording from reaching transcription.
        }
        lock (_sessionLock)
        {
            _stoppingSession = null;
            try
            {
                ReplaceWarmInput(preference);
            }
            catch (Exception error)
            {
                LogCaptureFailure($"Microphone could not be readied for the next recording: {error.Message}");
            }
        }
        return finalization;
    }

    public async Task<bool> WaitForFirstAudioAsync(TimeSpan timeout)
    {
        CaptureSession? session;
        lock (_sessionLock)
        {
            session = _activeSession ?? _stoppingSession;
        }

        if (session?.HasReceivedAudio == true)
        {
            return true;
        }

        try
        {
            return await _warmAudioSource.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            _activeSession?.Dispose();
            _activeSession = null;
            _stoppingSession?.Dispose();
            _stoppingSession = null;
            DisposeWarmInput();
        }
    }

    private void EnsureWarmInput(string? preferredInputDeviceName)
    {
        var preference = preferredInputDeviceName?.Trim() ?? "";
        if (_warmInput is not null
            && _warmAvailable
            && string.Equals(_warmPreference, preference, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ReplaceWarmInput(preference);
    }

    private void ReplaceWarmInput(string? preferredInputDeviceName)
    {
        DisposeWarmInput();
        var preference = preferredInputDeviceName?.Trim() ?? "";
        var deviceIndex = MicrophoneDeviceCatalog.ResolveIndex(preference);
        if (preference.Length > 0 && !deviceIndex.HasValue)
        {
            throw new InvalidOperationException($"Selected microphone is disconnected: {preference}");
        }

        var input = _createInputDevice(deviceIndex);
        var generation = ++_warmGeneration;
        _warmDataHandler = (_, args) => OnWarmDataAvailable(generation, args);
        _warmStoppedHandler = (_, args) => OnWarmRecordingStopped(generation, args);
        input.DataAvailable += _warmDataHandler;
        input.RecordingStopped += _warmStoppedHandler;
        try
        {
            _warmInput = input;
            _warmPreference = preference;
            _warmAvailable = true;
            ActiveInputDeviceName = deviceIndex.HasValue
                ? MicrophoneDeviceCatalog.ListDevices().FirstOrDefault(device => device.Index == deviceIndex.Value)?.Name
                : "Windows default";
            _warmDeviceDescription = CaptureDeviceDescription();
            input.StartRecording();
        }
        catch
        {
            DisposeWarmInput();
            throw;
        }
    }

    private void DisposeWarmInput()
    {
        var input = _warmInput;
        _warmInput = null;
        _warmAvailable = false;
        _warmGeneration++;
        if (input is not null)
        {
            if (_warmDataHandler is not null) input.DataAvailable -= _warmDataHandler;
            if (_warmStoppedHandler is not null) input.RecordingStopped -= _warmStoppedHandler;
            try { input.StopRecording(); } catch { }
            input.Dispose();
        }

        _warmDataHandler = null;
        _warmStoppedHandler = null;
        _preRoll.Clear();
        _warmAudioSource = NewAudioSource();
        _lastCallbackTimestamp = 0;
    }

    private void OnWarmDataAvailable(int generation, WaveInEventArgs e)
    {
        CaptureSession? session;
        long sequence;
        double callbackGapMs;
        lock (_sessionLock)
        {
            if (generation != _warmGeneration)
            {
                return;
            }

            var timestamp = Stopwatch.GetTimestamp();
            callbackGapMs = _lastCallbackTimestamp == 0
                ? 0
                : Stopwatch.GetElapsedTime(_lastCallbackTimestamp, timestamp).TotalMilliseconds;
            _lastCallbackTimestamp = timestamp;
            sequence = ++_callbackSequence;
            foreach (var value in e.Buffer.AsSpan(0, e.BytesRecorded))
            {
                if (_preRoll.Count == PreRollBytes)
                {
                    _preRoll.Dequeue();
                }
                _preRoll.Enqueue(value);
            }

            _warmAudioSource.TrySetResult(true);
            session = _activeSession ?? _stoppingSession;
        }

        session?.Write(e.Buffer, e.BytesRecorded, sequence, callbackGapMs);
    }

    private string CaptureDeviceDescription()
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var endpoint = devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            using var client = endpoint.CreateAudioClient();
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.ID)))[..12];
            return $"{ActiveInputDeviceName};default={endpoint.FriendlyName};endpoint={id};mix={client.MixFormat}";
        }
        catch
        {
            return ActiveInputDeviceName ?? "unknown";
        }
    }

    private void OnWarmRecordingStopped(int generation, StoppedEventArgs e)
    {
        CaptureSession? session;
        lock (_sessionLock)
        {
            if (generation != _warmGeneration)
            {
                return;
            }

            _warmAvailable = false;
            session = _activeSession ?? _stoppingSession;
        }

        session?.SignalStopped(e.Exception);
        if (e.Exception is not null)
        {
            LogCaptureFailure($"Microphone disconnected: {e.Exception.Message}");
        }
    }

    private static TaskCompletionSource<bool> NewAudioSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void PublishAudioChunk(byte[] audioChunk, double audioLevel)
    {
        AudioChunkAvailable?.Invoke(audioChunk);
        AudioLevelChanged?.Invoke(audioLevel);
    }

    private static void LogCaptureFailure(string message)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LafazFlow",
                "Logs");
            BoundedLogFileWriter.AppendLine(
                Path.Combine(logRoot, "lafazflow.log"),
                $"[{DateTimeOffset.Now:O}] {message}");
        }
        catch
        {
        }
    }

    private sealed class CaptureSession : IDisposable
    {
        private readonly object _lock = new();
        private readonly IAudioCaptureWriter _writer;
        private readonly Action<byte[], double> _publishAudioChunk;
        private readonly TaskCompletionSource<bool> _drainedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _outputPath;
        private readonly long _startCallbackSequence;
        private readonly int _preRollBytes;
        private readonly string _device;
        private readonly WaveFormat _format;
        private long _firstCallbackSequence;
        private long _lastCallbackSequence;
        private int _callbackCount;
        private int _lateCallbackCount;
        private int _misalignedCallbackCount;
        private double _maxCallbackGapMs;
        private bool _active = true;
        private bool _hasReceivedAudio;
        private long _writtenBytes;
        private Exception? _deviceError;

        public AudioCaptureState State { get; private set; } = AudioCaptureState.Recording;

        public bool HasReceivedAudio => _hasReceivedAudio;

        public CaptureSession(
            IAudioCaptureWriter writer,
            Action<byte[], double> publishAudioChunk,
            string outputPath,
            byte[] preRoll,
            long startCallbackSequence,
            string device,
            WaveFormat format)
        {
            _writer = writer;
            _publishAudioChunk = publishAudioChunk;
            _outputPath = outputPath;
            _startCallbackSequence = startCallbackSequence;
            _preRollBytes = preRoll.Length;
            _device = device;
            _format = format;
            if (preRoll.Length > 0)
            {
                _writer.Write(preRoll, 0, preRoll.Length);
                _writtenBytes = preRoll.Length;
            }
        }

        public async Task<AudioCaptureFinalization> StopAsync(TimeSpan deadline)
        {
            lock (_lock)
            {
                if (State != AudioCaptureState.Recording)
                {
                    throw new InvalidOperationException("Recording is not active.");
                }

                State = AudioCaptureState.Stopping;
            }

            var timedOut = false;
            try
            {
                await _drainedSource.Task.WaitAsync(deadline);
            }
            catch (TimeoutException)
            {
                timedOut = true;
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
            }

            return Finalize(timedOut);
        }

        private AudioCaptureFinalization Finalize(bool timedOut)
        {
            string errorKind = timedOut ? "audio_drain_timeout" : "";
            lock (_lock)
            {
                if (_active)
                {
                    _active = false;
                }

                try
                {
                    _writer.Dispose();
                }
                catch
                {
                    errorKind = string.IsNullOrWhiteSpace(errorKind) ? "writer_failure" : $"{errorKind}|writer_failure";
                }

                if (_deviceError is not null)
                {
                    errorKind = string.IsNullOrWhiteSpace(errorKind)
                        ? "device_error"
                        : $"{errorKind}|device_error";
                }

                var sampleCount = _writtenBytes / 2;
                var durationMilliseconds = sampleCount * 1000 / 16000;
                State = string.IsNullOrWhiteSpace(errorKind)
                    || errorKind.Equals("audio_drain_timeout", StringComparison.Ordinal)
                    ? AudioCaptureState.Finalized
                    : AudioCaptureState.Failed;
                return new AudioCaptureFinalization(
                    _outputPath,
                    sampleCount,
                    _writtenBytes,
                    durationMilliseconds,
                    State == AudioCaptureState.Failed
                        ? AudioCaptureFinalizeState.Failed
                        : AudioCaptureFinalizeState.Finalized,
                    errorKind);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_active)
                {
                    return;
                }

                _active = false;
                _writer.Dispose();
            }
        }

        public void SignalStopped(Exception? error)
        {
            lock (_lock)
            {
                _deviceError = error;
                _drainedSource.TrySetResult(true);
            }
        }

        public string DiagnosticSummary(AudioCaptureFinalization finalization)
        {
            lock (_lock)
            {
                var signal = CaptureOnsetMetrics.Analyze(_outputPath, _preRollBytes);
                return $"CAPTURE state={finalization.State} device={_device} format={_format} " +
                    $"start_seq={_startCallbackSequence} first_seq={_firstCallbackSequence} last_seq={_lastCallbackSequence} " +
                    $"callbacks={_callbackCount} pre_roll_bytes={_preRollBytes} max_callback_gap_ms={_maxCallbackGapMs:F1} " +
                    $"late_callbacks={_lateCallbackCount} misaligned_callbacks={_misalignedCallbackCount} " +
                    $"first_above_0_1pct_sample={signal.FirstAbovePointOnePercentSample} " +
                    $"first_above_1pct_sample={signal.FirstAboveOnePercentSample} " +
                    $"boundary_jump_pcm={signal.BoundaryJumpPcm} " +
                    $"onset_100ms_rms={signal.RmsFirst100:F4} onset_100ms_peak={signal.PeakFirst100:F4} " +
                    $"onset_500ms_rms={signal.RmsFirst500:F4} onset_500ms_peak={signal.PeakFirst500:F4}";
            }
        }

        public void Write(byte[] buffer, int bytesRecorded, long sequence, double callbackGapMs)
        {
            byte[] audioChunk;
            double audioLevel;
            lock (_lock)
            {
                if (!_active)
                {
                    return;
                }

                if (_callbackCount == 0) _firstCallbackSequence = sequence;
                _lastCallbackSequence = sequence;
                _callbackCount++;
                _maxCallbackGapMs = Math.Max(_maxCallbackGapMs, callbackGapMs);
                var expectedMs = bytesRecorded * 1000.0 / _format.AverageBytesPerSecond;
                if (callbackGapMs > expectedMs + 25) _lateCallbackCount++;
                if (bytesRecorded % _format.BlockAlign != 0) _misalignedCallbackCount++;
                _hasReceivedAudio = true;
                _writer.Write(buffer, 0, bytesRecorded);
                _writtenBytes += bytesRecorded;
                audioChunk = new byte[bytesRecorded];
                Buffer.BlockCopy(buffer, 0, audioChunk, 0, bytesRecorded);
                audioLevel = CalculateAudioLevel(buffer, bytesRecorded);
            }

            _publishAudioChunk(audioChunk, audioLevel);
        }

        private static double CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            var max = 0;
            for (var index = 0; index < bytesRecorded; index += 2)
            {
                var sample = BitConverter.ToInt16(buffer, index);
                max = Math.Max(max, Math.Abs(sample));
            }

            return Math.Clamp(max / 32768.0, 0, 1);
        }
    }
}

internal interface IAudioInputDevice : IDisposable
{
    event EventHandler<WaveInEventArgs>? DataAvailable;

    event EventHandler<StoppedEventArgs>? RecordingStopped;

    WaveFormat WaveFormat { get; }

    void StartRecording();

    void StopRecording();
}

internal interface IAudioCaptureWriter : IDisposable
{
    void Write(byte[] buffer, int offset, int count);
}

internal sealed class WasapiAudioInputDevice : IAudioInputDevice
{
    private readonly WasapiRecorder _capture;
    private readonly NativePcm16Resampler _converter;
    private readonly object _traceLock = new();
    private WaveFileWriter? _nativeTrace;
    private int _traceBytesRemaining;

    public WasapiAudioInputDevice(int deviceIndex = -1)
    {
        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var endpoint = deviceIndex < 0
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
            : endpoints[deviceIndex];
        _capture = new WasapiRecorderBuilder()
            .WithDevice(endpoint)
            .WithRawMode()
            .WithBufferLength(50)
            .WithMmcssThreadPriority("Audio")
            .Build();
        try { _converter = new NativePcm16Resampler(_capture.WaveFormat); }
        catch { _capture.Dispose(); throw; }
        _capture.DataAvailable += (buffer, _, _, _) =>
        {
            var native = buffer.ToArray();
            lock (_traceLock)
            {
                if (_nativeTrace is not null && _traceBytesRemaining > 0)
                {
                    try
                    {
                        var count = Math.Min(native.Length, _traceBytesRemaining);
                        _nativeTrace.Write(native, 0, count);
                        _traceBytesRemaining -= count;
                    }
                    catch
                    {
                        try { _nativeTrace.Dispose(); } catch { }
                        _nativeTrace = null;
                    }
                }
            }
            _converter.Add(native, native.Length, Publish);
        };
        _capture.RecordingStopped += (_, args) =>
        {
            lock (_traceLock)
            {
                try { _nativeTrace?.Dispose(); } catch { }
                _nativeTrace = null;
            }
            var stopped = args;
            try
            {
                if (args.Exception is null) _converter.Flush(Publish);
            }
            catch (Exception error)
            {
                stopped = new StoppedEventArgs(error);
            }
            RecordingStopped?.Invoke(this, stopped);
        };
    }

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public WaveFormat WaveFormat { get; } = new(16000, 16, 1);

    public void StartRecording() => _capture.StartRecording();

    public void StopRecording() => _capture.StopRecording();

    public void Dispose()
    {
        _capture.Dispose();
        lock (_traceLock)
        {
            _nativeTrace?.Dispose();
            _nativeTrace = null;
        }
    }

    public void BeginNativeTrace(string outputPath)
    {
        var folder = Path.Combine(Path.GetDirectoryName(outputPath)!, "NativeDebug");
        Directory.CreateDirectory(folder);
        if (Directory.EnumerateFiles(folder, "*.wav").Take(30).Count() == 30) return;
        lock (_traceLock)
        {
            _nativeTrace?.Dispose();
            _nativeTrace = new WaveFileWriter(Path.Combine(folder, Path.GetFileName(outputPath)), _capture.WaveFormat);
            _traceBytesRemaining = _capture.WaveFormat.AverageBytesPerSecond * 5;
        }
    }

    private void Publish(byte[] pcm) => DataAvailable?.Invoke(this, new WaveInEventArgs(pcm, pcm.Length));
}

internal sealed class NativePcm16Resampler
{
    private readonly BufferedWaveProvider _nativeBuffer;
    private readonly WdlResamplingSampleProvider _resampler;
    private readonly byte[] _flushBuffer;
    private readonly float[] _samples = new float[3200];

    public NativePcm16Resampler(WaveFormat nativeFormat)
    {
        if (nativeFormat.Channels is < 1 or > 2)
            throw new NotSupportedException($"Unsupported microphone channel count: {nativeFormat.Channels}");

        _flushBuffer = new byte[(nativeFormat.AverageBytesPerSecond / 20 / nativeFormat.BlockAlign) * nativeFormat.BlockAlign];
        _nativeBuffer = new BufferedWaveProvider(nativeFormat)
        {
            ReadFully = false
        };
        ISampleProvider samples = _nativeBuffer.ToSampleProvider();
        if (nativeFormat.Channels == 2)
        {
            samples = new StereoToMonoSampleProvider(samples) { LeftVolume = 0.5f, RightVolume = 0.5f };
        }
        _resampler = new WdlResamplingSampleProvider(samples, 16000);
    }

    public void Add(byte[] nativePcm, int count, Action<byte[]> publish)
    {
        _nativeBuffer.AddSamples(nativePcm, 0, count);
        Drain(publish);
    }

    public void Flush(Action<byte[]> publish) => Add(_flushBuffer, _flushBuffer.Length, publish);

    private void Drain(Action<byte[]> publish)
    {
        int count;
        while ((count = _resampler.Read(_samples)) > 0)
        {
            var pcm = new byte[count * 2];
            for (var index = 0; index < count; index++)
            {
                var value = (short)Math.Clamp((int)Math.Round(_samples[index] * 32768), short.MinValue, short.MaxValue);
                BitConverter.TryWriteBytes(pcm.AsSpan(index * 2, 2), value);
            }
            publish(pcm);
        }
    }
}

internal sealed class WaveFileAudioCaptureWriter : IAudioCaptureWriter
{
    private readonly WaveFileWriter _writer;
    private long _writtenBytes;

    public long WrittenBytes => _writtenBytes;

    public WaveFileAudioCaptureWriter(string path, WaveFormat format)
    {
        _writer = new WaveFileWriter(path, format);
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        _writer.Write(buffer, offset, count);
        _writtenBytes += count;
    }

    public void Dispose() => _writer.Dispose();
}
