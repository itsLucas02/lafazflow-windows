using System.IO;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;

    public event Action<double>? AudioLevelChanged;

    public string Start(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}.wav");

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
        _writer = new WaveFileWriter(outputPath, _waveIn.WaveFormat);
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        return outputPath;
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;

        _writer?.Dispose();
        _writer = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

        var max = 0;
        for (var index = 0; index < e.BytesRecorded; index += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, index);
            max = Math.Max(max, Math.Abs(sample));
        }

        AudioLevelChanged?.Invoke(Math.Clamp(max / 32768.0, 0, 1));
    }
}
