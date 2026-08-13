using LafazFlow.Windows.Services;
using NAudio.Wave;

namespace LafazFlow.Windows.Tests;

public sealed class AudioCaptureServiceTests
{
    [Fact]
    public async Task StopAsyncIncludesFinalBufferArrivingAfterStopRequest()
    {
        var input = new FakeAudioInputDevice { FireStopOnStop = false };
        var writer = new FakeAudioCaptureWriter();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            using var service = new AudioCaptureService(
                () => input,
                (_, _) => writer,
                TimeSpan.FromMilliseconds(200));
            service.Start(root);
            input.Emit(new byte[1600]);

            var stopTask = service.StopAsync();
            input.Emit(new byte[1600]);
            input.EmitStop();
            var finalization = await stopTask;

            Assert.Equal(AudioCaptureFinalizeState.Finalized, finalization.State);
            Assert.Equal("", finalization.ErrorKind);
            Assert.Equal(1600, finalization.SampleCount);
            Assert.Equal(3200, finalization.ByteCount);
            Assert.Equal(3200, writer.Bytes.Length);
            Assert.Equal(100, finalization.DurationMilliseconds);
            Assert.True(input.Stopped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StoppedSessionCannotWriteIntoNextRecording()
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
            await service.StopAsync();

            service.Start(root);
            lateFirstCallback(null, new WaveInEventArgs([9, 0, 9, 0], 4));
            secondInput.Emit([3, 0, 4, 0]);
            await service.StopAsync();

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
    public async Task StartRejectsASecondActiveSessionWithoutReplacingIt()
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
            await service.StopAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StopTimeoutFinalizesReceivedAudio()
    {
        var input = new FakeAudioInputDevice { FireStopOnStop = false };
        var writer = new FakeAudioCaptureWriter();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            using var service = new AudioCaptureService(
                () => input,
                (_, _) => writer,
                TimeSpan.FromMilliseconds(40));
            service.Start(root);
            input.Emit(new byte[3200]);

            var finalization = await service.StopAsync();

            Assert.Equal(AudioCaptureFinalizeState.Finalized, finalization.State);
            Assert.Equal("audio_drain_timeout", finalization.ErrorKind);
            Assert.Equal(3200, writer.Bytes.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriterFailureMarksFinalizationFailed()
    {
        var input = new FakeAudioInputDevice();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            using var service = new AudioCaptureService(
                () => input,
                (_, _) => new ThrowingAudioCaptureWriter(),
                TimeSpan.FromMilliseconds(200));
            service.Start(root);
            input.Emit([1, 0]);

            var finalization = await service.StopAsync();

            Assert.Equal(AudioCaptureFinalizeState.Failed, finalization.State);
            Assert.Contains("writer_failure", finalization.ErrorKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeviceErrorFinalizesWithErrorKind()
    {
        var input = new FakeAudioInputDevice
        {
            FireStopOnStop = true,
            StopError = new InvalidOperationException("device removed")
        };
        var writer = new FakeAudioCaptureWriter();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            using var service = new AudioCaptureService(() => input, (_, _) => writer);
            service.Start(root);
            input.Emit([1, 0]);

            var finalization = await service.StopAsync();

            Assert.Equal(AudioCaptureFinalizeState.Finalized, finalization.State);
            Assert.Contains("device_error", finalization.ErrorKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StopAsyncWithoutActiveSessionThrows()
    {
        using var service = new AudioCaptureService(() => new FakeAudioInputDevice(), (_, _) => new FakeAudioCaptureWriter());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StopAsync());

        Assert.Equal("No active microphone recording.", error.Message);
    }

    [Fact]
    public async Task RealWriterProducesHeaderSampleAndDurationParity()
    {
        var input = new FakeAudioInputDevice();
        var root = Directory.CreateTempSubdirectory("LafazFlowAudioCapture-").FullName;
        try
        {
            string? outputPath = null;
            using var service = new AudioCaptureService(
                () => input,
                (path, format) => new WaveFileAudioCaptureWriter(path, format),
                TimeSpan.FromMilliseconds(200));
            outputPath = service.Start(root);
            input.Emit(new byte[3200]);

            var finalization = await service.StopAsync();
            var wav = WavFileValidator.Inspect(outputPath);

            Assert.NotNull(wav);
            Assert.Equal(AudioCaptureFinalizeState.Finalized, finalization.State);
            Assert.Equal(3200, finalization.ByteCount);
            Assert.Equal(1600, finalization.SampleCount);
            Assert.Equal(1600, wav.SampleCount);
            Assert.Equal(3200, wav.DataSize);
            Assert.Equal(100, wav.DurationMilliseconds);
            Assert.Equal(100, finalization.DurationMilliseconds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeAudioInputDevice : IAudioInputDevice
    {
        public event EventHandler<WaveInEventArgs>? DataAvailable;

        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public WaveFormat WaveFormat { get; } = new(16000, 16, 1);

        public bool Stopped { get; private set; }

        public bool FireStopOnStop { get; init; } = true;

        public Exception? StopError { get; init; }

        public void StartRecording()
        {
        }

        public void StopRecording()
        {
            Stopped = true;
            if (FireStopOnStop)
            {
                RecordingStopped?.Invoke(this, new StoppedEventArgs(StopError));
            }
        }

        public EventHandler<WaveInEventArgs> CaptureDataCallback()
        {
            return DataAvailable ?? throw new InvalidOperationException("No data callback registered.");
        }

        public void Emit(byte[] bytes)
        {
            DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
        }

        public void EmitStop()
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(StopError));
        }

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingAudioCaptureWriter : IAudioCaptureWriter
    {
        public void Write(byte[] buffer, int offset, int count)
        {
        }

        public void Dispose()
        {
            throw new IOException("disk full");
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
