using System.IO;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private static readonly TimeSpan DefaultStopDeadline = TimeSpan.FromSeconds(2);

    private readonly object _sessionLock = new();
    private readonly Func<int?, IAudioInputDevice> _createInputDevice;
    private readonly Func<string, WaveFormat, IAudioCaptureWriter> _createWriter;
    private readonly TimeSpan _stopDeadline;
    private CaptureSession? _activeSession;

    public AudioCaptureService()
        : this(
            _ => new WaveInAudioInputDevice(_ ?? -1),
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

            var deviceIndex = MicrophoneDeviceCatalog.ResolveIndex(preferredInputDeviceName);
            var input = _createInputDevice(deviceIndex);
            var writer = _createWriter(outputPath, input.WaveFormat);
            session = new CaptureSession(input, writer, PublishAudioChunk, outputPath);
            _activeSession = session;
            ActiveInputDeviceName = deviceIndex.HasValue
                ? MicrophoneDeviceCatalog.ListDevices().FirstOrDefault(device => device.Index == deviceIndex.Value)?.Name
                : null;
        }

        try
        {
            session.Start();
            return outputPath;
        }
        catch
        {
            lock (_sessionLock)
            {
                if (ReferenceEquals(_activeSession, session))
                {
                    _activeSession = null;
                }
            }

            session.Dispose();
            throw;
        }
    }

    public async Task<AudioCaptureFinalization> StopAsync()
    {
        CaptureSession? session;
        lock (_sessionLock)
        {
            session = _activeSession;
            _activeSession = null;
        }

        if (session is null)
        {
            throw new InvalidOperationException("No active microphone recording.");
        }

        return await session.StopAsync(_stopDeadline);
    }

    public async Task<bool> WaitForFirstAudioAsync(TimeSpan timeout)
    {
        CaptureSession? session;
        lock (_sessionLock)
        {
            session = _activeSession;
        }

        return session is not null && await session.WaitForFirstAudioAsync(timeout);
    }

    public bool TrySwitchInputDevice(int deviceIndex, out string deviceName)
    {
        deviceName = MicrophoneDeviceCatalog.ListDevices()
            .FirstOrDefault(device => device.Index == deviceIndex)?.Name
            ?? $"Microphone #{deviceIndex}";
        CaptureSession? session;
        lock (_sessionLock)
        {
            session = _activeSession;
        }

        if (session is null)
        {
            return false;
        }

        var replacement = _createInputDevice(deviceIndex);
        if (!session.SwitchInput(replacement))
        {
            replacement.Dispose();
            return false;
        }

        ActiveInputDeviceName = deviceName;
        LogCaptureFailure($"Capture switched to input device {deviceIndex} ({deviceName}).");
        return true;
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            _activeSession?.Dispose();
            _activeSession = null;
        }
    }

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
        private IAudioInputDevice _input;
        private readonly IAudioCaptureWriter _writer;
        private readonly Action<byte[], double> _publishAudioChunk;
        private readonly TaskCompletionSource<StoppedEventArgs?> _stoppedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstAudioSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _outputPath;
        private bool _active = true;
        private bool _hasReceivedAudio;
        private long _writtenBytes;

        public AudioCaptureState State { get; private set; } = AudioCaptureState.Recording;

        public bool HasReceivedAudio => _hasReceivedAudio;

        public CaptureSession(
            IAudioInputDevice input,
            IAudioCaptureWriter writer,
            Action<byte[], double> publishAudioChunk,
            string outputPath)
        {
            _input = input;
            _writer = writer;
            _publishAudioChunk = publishAudioChunk;
            _outputPath = outputPath;
            _input.DataAvailable += OnDataAvailable;
            _input.RecordingStopped += OnRecordingStopped;
        }

        public void Start()
        {
            _input.StartRecording();
        }

        public async Task<bool> WaitForFirstAudioAsync(TimeSpan timeout)
        {
            if (_hasReceivedAudio)
            {
                return true;
            }

            try
            {
                return await _firstAudioSource.Task.WaitAsync(timeout);
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

        public bool SwitchInput(IAudioInputDevice replacement)
        {
            lock (_lock)
            {
                if (!_active || State != AudioCaptureState.Recording)
                {
                    return false;
                }

                if (replacement.WaveFormat.SampleRate != _input.WaveFormat.SampleRate
                    || replacement.WaveFormat.BitsPerSample != _input.WaveFormat.BitsPerSample
                    || replacement.WaveFormat.Channels != _input.WaveFormat.Channels)
                {
                    return false;
                }

                _input.DataAvailable -= OnDataAvailable;
                _input.RecordingStopped -= OnRecordingStopped;
                try
                {
                    _input.StopRecording();
                }
                catch
                {
                }

                _input.Dispose();
                _input = replacement;
                _input.DataAvailable += OnDataAvailable;
                _input.RecordingStopped += OnRecordingStopped;
                try
                {
                    _input.StartRecording();
                    return true;
                }
                catch
                {
                    return false;
                }
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

            try
            {
                _input.StopRecording();
            }
            catch
            {
                // Fall through to the bounded deadline path.
            }

            var timedOut = false;
            StoppedEventArgs? stopped = null;
            try
            {
                stopped = await _stoppedSource.Task.WaitAsync(deadline);
            }
            catch (TimeoutException)
            {
                timedOut = true;
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
            }

            return Finalize(stopped, timedOut);
        }

        private AudioCaptureFinalization Finalize(StoppedEventArgs? stopped, bool timedOut)
        {
            string errorKind = timedOut ? "audio_drain_timeout" : "";
            lock (_lock)
            {
                if (_active)
                {
                    _active = false;
                    _input.DataAvailable -= OnDataAvailable;
                    _input.RecordingStopped -= OnRecordingStopped;
                }

                try
                {
                    _input.StopRecording();
                }
                catch
                {
                }

                try
                {
                    _writer.Dispose();
                }
                catch
                {
                    errorKind = string.IsNullOrWhiteSpace(errorKind) ? "writer_failure" : $"{errorKind}|writer_failure";
                }

                try
                {
                    _input.Dispose();
                }
                catch
                {
                }

                if (stopped?.Exception is { } deviceException)
                {
                    errorKind = string.IsNullOrWhiteSpace(errorKind)
                        ? "device_error"
                        : $"{errorKind}|device_error";
                    LogCaptureFailure($"Device reported an error during stop: {deviceException.Message}");
                }

                var sampleCount = _writtenBytes / 2;
                var durationMilliseconds = sampleCount * 1000 / 16000;
                State = errorKind.Contains("writer_failure", StringComparison.Ordinal)
                    ? AudioCaptureState.Failed
                    : AudioCaptureState.Finalized;
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
                _input.DataAvailable -= OnDataAvailable;
                _input.RecordingStopped -= OnRecordingStopped;
                try
                {
                    _input.StopRecording();
                }
                finally
                {
                    _input.Dispose();
                    _writer.Dispose();
                }
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _stoppedSource.TrySetResult(e);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            byte[] audioChunk;
            double audioLevel;
            lock (_lock)
            {
                if (!_active)
                {
                    return;
                }

                _hasReceivedAudio = true;
                _firstAudioSource.TrySetResult(true);
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
                _writtenBytes += e.BytesRecorded;
                audioChunk = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, audioChunk, 0, e.BytesRecorded);
                audioLevel = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
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

internal sealed class WaveInAudioInputDevice : IAudioInputDevice
{
    private readonly WaveInEvent _waveIn;

    public WaveInAudioInputDevice(int deviceIndex = -1)
    {
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
    }

    public event EventHandler<WaveInEventArgs>? DataAvailable
    {
        add => _waveIn.DataAvailable += value;
        remove => _waveIn.DataAvailable -= value;
    }

    public event EventHandler<StoppedEventArgs>? RecordingStopped
    {
        add => _waveIn.RecordingStopped += value;
        remove => _waveIn.RecordingStopped -= value;
    }

    public WaveFormat WaveFormat => _waveIn.WaveFormat;

    public void StartRecording() => _waveIn.StartRecording();

    public void StopRecording() => _waveIn.StopRecording();

    public void Dispose() => _waveIn.Dispose();
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
