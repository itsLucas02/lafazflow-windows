using LafazFlow.Windows.Services;
using NAudio.Wave;

namespace LafazFlow.Windows.Tests;

public sealed class AudioCaptureServiceTests
{
    [Fact]
    public void StoppedSessionCannotWriteIntoNextRecording()
    {
        var firstInput = new FakeAudioInputDevice();
        var secondInput = new FakeAudioInputDevice();
        var inputs = new Queue<IAudioInputDevice>([firstInput, secondInput]);
        var writers = new List<FakeAudioCaptureWriter>();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            using var service = new AudioCaptureService(
                () => inputs.Dequeue(),
                (_, _) =>
                {
                    var writer = new FakeAudioCaptureWriter();
                    writers.Add(writer);
                    return writer;
                });

            service.Start(root);
            var lateFirstCallback = firstInput.CaptureDataCallback();
            firstInput.Emit([1, 0, 2, 0]);
            service.Stop();

            service.Start(root);
            lateFirstCallback(null, new WaveInEventArgs([9, 0, 9, 0], 4));
            secondInput.Emit([3, 0, 4, 0]);
            service.Stop();

            Assert.Equal([1, 0, 2, 0], writers[0].Bytes);
            Assert.Equal([3, 0, 4, 0], writers[1].Bytes);
            Assert.True(firstInput.Stopped);
            Assert.True(secondInput.Stopped);
            Assert.All(writers, writer => Assert.True(writer.Disposed));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartRejectsASecondActiveSessionWithoutReplacingIt()
    {
        var input = new FakeAudioInputDevice();
        var writer = new FakeAudioCaptureWriter();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            using var service = new AudioCaptureService(() => input, (_, _) => writer);
            service.Start(root);

            var error = Assert.Throws<InvalidOperationException>(() => service.Start(root));

            Assert.Equal("A microphone recording is already active.", error.Message);
            input.Emit([5, 0]);
            Assert.Equal([5, 0], writer.Bytes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeAudioInputDevice : IAudioInputDevice
    {
        public event EventHandler<WaveInEventArgs>? DataAvailable;

        public WaveFormat WaveFormat { get; } = new(16000, 16, 1);

        public bool Stopped { get; private set; }

        public void StartRecording()
        {
        }

        public void StopRecording()
        {
            Stopped = true;
        }

        public EventHandler<WaveInEventArgs> CaptureDataCallback()
        {
            return DataAvailable ?? throw new InvalidOperationException("No data callback registered.");
        }

        public void Emit(byte[] bytes)
        {
            DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeAudioCaptureWriter : IAudioCaptureWriter
    {
        private readonly List<byte> _bytes = [];

        public byte[] Bytes => [.. _bytes];

        public bool Disposed { get; private set; }

        public void Write(byte[] buffer, int offset, int count)
        {
            _bytes.AddRange(buffer.Skip(offset).Take(count));
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
