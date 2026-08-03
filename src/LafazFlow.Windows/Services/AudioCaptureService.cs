using System.IO;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly object _sessionLock = new();
    private readonly Func<IAudioInputDevice> _createInputDevice;
    private readonly Func<string, WaveFormat, IAudioCaptureWriter> _createWriter;
    private CaptureSession? _activeSession;

    public AudioCaptureService()
        : this(
            () => new WaveInAudioInputDevice(),
            (path, format) => new WaveFileAudioCaptureWriter(path, format))
    {
    }

    internal AudioCaptureService(
        Func<IAudioInputDevice> createInputDevice,
        Func<string, WaveFormat, IAudioCaptureWriter> createWriter)
    {
        _createInputDevice = createInputDevice;
        _createWriter = createWriter;
    }

    public event Action<double>? AudioLevelChanged;

    public event Action<byte[]>? AudioChunkAvailable;

    public string Start(string outputDirectory)
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

            var input = _createInputDevice();
            var writer = _createWriter(outputPath, input.WaveFormat);
            session = new CaptureSession(input, writer, PublishAudioChunk);
            _activeSession = session;
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

    public void Stop()
    {
        CaptureSession? session;
        lock (_sessionLock)
        {
            session = _activeSession;
            _activeSession = null;
        }

        session?.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }

    private void PublishAudioChunk(byte[] audioChunk, double audioLevel)
    {
        AudioChunkAvailable?.Invoke(audioChunk);
        AudioLevelChanged?.Invoke(audioLevel);
    }

    private sealed class CaptureSession : IDisposable
    {
        private readonly object _lock = new();
        private readonly IAudioInputDevice _input;
        private readonly IAudioCaptureWriter _writer;
        private readonly Action<byte[], double> _publishAudioChunk;
        private bool _active = true;

        public CaptureSession(
            IAudioInputDevice input,
            IAudioCaptureWriter writer,
            Action<byte[], double> publishAudioChunk)
        {
            _input = input;
            _writer = writer;
            _publishAudioChunk = publishAudioChunk;
            _input.DataAvailable += OnDataAvailable;
        }

        public void Start()
        {
            _input.StartRecording();
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

                _writer.Write(e.Buffer, 0, e.BytesRecorded);
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
    private readonly WaveInEvent _waveIn = new()
    {
        DeviceNumber = -1,
        WaveFormat = new WaveFormat(16000, 16, 1),
        BufferMilliseconds = 50
    };

    public event EventHandler<WaveInEventArgs>? DataAvailable
    {
        add => _waveIn.DataAvailable += value;
        remove => _waveIn.DataAvailable -= value;
    }

    public WaveFormat WaveFormat => _waveIn.WaveFormat;

    public void StartRecording() => _waveIn.StartRecording();

    public void StopRecording() => _waveIn.StopRecording();

    public void Dispose() => _waveIn.Dispose();
}

internal sealed class WaveFileAudioCaptureWriter : IAudioCaptureWriter
{
    private readonly WaveFileWriter _writer;

    public WaveFileAudioCaptureWriter(string path, WaveFormat format)
    {
        _writer = new WaveFileWriter(path, format);
    }

    public void Write(byte[] buffer, int offset, int count) => _writer.Write(buffer, offset, count);

    public void Dispose() => _writer.Dispose();
}
