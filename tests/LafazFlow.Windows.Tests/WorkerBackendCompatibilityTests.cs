using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WorkerBackendCompatibilityTests
{
    [Fact]
    public async Task CudaSettingsWithCudaWorkerSucceeds()
    {
        var (supervisor, engine) = CreateEngine("cuda", "cuda");
        var audioPath = WriteToneWav();

        try
        {
            var result = await engine.TranscribeAsync(
                audioPath,
                CudaSettings,
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal("Fake transcript.", result.Text);
        }
        finally
        {
            await supervisor.ShutdownAsync();
            supervisor.Dispose();
        }
    }

    [Fact]
    public async Task CudaSettingsRejectsCpuWorker()
    {
        var (supervisor, engine) = CreateEngine("cpu", "cpu");
        var audioPath = WriteToneWav();

        try
        {
            var result = await engine.TranscribeAsync(
                audioPath,
                CudaSettings,
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("worker_backend_mismatch", result.FailureKind);
        }
        finally
        {
            await supervisor.ShutdownAsync();
            supervisor.Dispose();
        }
    }

    [Fact]
    public async Task FastCpuSettingsWithCpuWorkerSucceeds()
    {
        var (supervisor, engine) = CreateEngine("cpu", "cpu");
        var audioPath = WriteToneWav();

        try
        {
            var result = await engine.TranscribeAsync(
                audioPath,
                AppSettings.Default,
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal("Fake transcript.", result.Text);
        }
        finally
        {
            await supervisor.ShutdownAsync();
            supervisor.Dispose();
        }
    }

    [Fact]
    public async Task FastCpuSettingsRejectsCudaWorker()
    {
        var (supervisor, engine) = CreateEngine("cuda", "cuda");
        var audioPath = WriteToneWav();

        try
        {
            var result = await engine.TranscribeAsync(
                audioPath,
                AppSettings.Default,
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("worker_backend_mismatch", result.FailureKind);
        }
        finally
        {
            await supervisor.ShutdownAsync();
            supervisor.Dispose();
        }
    }

    [Fact]
    public async Task FastCpuSettingsAcceptCudaCompiledWorkerRunningCpu()
    {
        var (supervisor, engine) = CreateEngine("cuda", "cpu");
        var audioPath = WriteToneWav();

        try
        {
            var result = await engine.TranscribeAsync(
                audioPath,
                AppSettings.Default,
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.True(result.Succeeded);
        }
        finally
        {
            await supervisor.ShutdownAsync();
            supervisor.Dispose();
        }
    }

    [Fact]
    public async Task MissingCudaRuntimeWithInvalidCliFailsClearly()
    {
        var (supervisor, primary) = CreateEngine("cpu", "cpu");
        var audioPath = WriteToneWav();
        var fallback = new CliTranscriptionEngine(
            new ThrowingTranscriptionService(
                "Whisper CLI was not found. CUDA compatibility requires a CUDA-enabled whisper-cli.exe and an NVIDIA GPU."));
        var recovering = new RecoveringTranscriptionEngine(
            primary,
            fallback,
            (_, _, _) => Task.CompletedTask);

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => recovering.TranscribeAsync(
                audioPath,
                CudaSettings,
                Guid.NewGuid(),
                CancellationToken.None));

            Assert.Contains("Whisper CLI was not found", error.Message);
            Assert.Contains("CUDA", error.Message);
        }
        finally
        {
            await supervisor.ShutdownAsync();
            supervisor.Dispose();
        }
    }

    [Fact]
    public void PolicyNeverSilentlyDowngradesCudaToCpu()
    {
        Assert.False(WhisperBackendPolicy.IsWorkerCompatible(CudaSettings, WhisperBackend.Cpu, WhisperBackend.Cpu));
        Assert.False(WhisperBackendPolicy.IsWorkerCompatible(CudaSettings, null, WhisperBackend.Cuda));
        Assert.True(WhisperBackendPolicy.IsWorkerCompatible(CudaSettings, WhisperBackend.Cuda, WhisperBackend.Cuda));
        Assert.Equal(WhisperBackend.Cuda, WhisperBackendPolicy.RequiredBackend(CudaSettings));
        Assert.Equal(WhisperBackend.Cpu, WhisperBackendPolicy.RequiredBackend(AppSettings.Default));
    }

    private static AppSettings CudaSettings => AppSettings.Default with
    {
        TranscriptionProfile = TranscriptionProfile.Quality,
        WhisperBackend = WhisperBackend.Cuda,
        EnableVad = true,
        WhisperThreads = 16
    };

    private static (WhisperWorkerSupervisor Supervisor, WorkerTranscriptionEngine Engine) CreateEngine(
        string compiledBackend,
        string runtimeBackend)
    {
        var factory = new FakeWorkerProcessFactory(
            () => { },
            compiledBackend: compiledBackend,
            runtimeBackend: runtimeBackend);
        var supervisor = new WhisperWorkerSupervisor(
            new WhisperWorkerSupervisorOptions
            {
                ReadinessTimeout = TimeSpan.FromSeconds(30),
                OperationTimeout = TimeSpan.FromSeconds(10),
                ShutdownTimeout = TimeSpan.FromSeconds(2)
            },
            () => factory.Create());
        return (supervisor, new WorkerTranscriptionEngine(supervisor));
    }

    private static string WriteToneWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lafazflow-backend-{Guid.NewGuid():N}.wav");
        var sampleCount = 16000;
        var dataSize = sampleCount * 2;
        var bytes = new byte[44 + dataSize];
        Buffer.BlockCopy("RIFF"u8.ToArray(), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(36 + dataSize), 0, bytes, 4, 4);
        Buffer.BlockCopy("WAVE"u8.ToArray(), 0, bytes, 8, 4);
        Buffer.BlockCopy("fmt "u8.ToArray(), 0, bytes, 12, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(16), 0, bytes, 16, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, bytes, 20, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, bytes, 22, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(16000), 0, bytes, 24, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(32000), 0, bytes, 28, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((short)2), 0, bytes, 32, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, bytes, 34, 2);
        Buffer.BlockCopy("data"u8.ToArray(), 0, bytes, 36, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(dataSize), 0, bytes, 40, 4);
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000.0) * short.MaxValue * 0.2);
            var b = BitConverter.GetBytes(sample);
            bytes[44 + (i * 2)] = b[0];
            bytes[44 + (i * 2) + 1] = b[1];
        }

        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class ThrowingTranscriptionService : ITranscriptionService
    {
        private readonly string _message;

        public ThrowingTranscriptionService(string message)
        {
            _message = message;
        }

        public Task<string> TranscribeAsync(
            string whisperCliPath,
            string modelPath,
            string audioPath,
            string prompt,
            int threads,
            WhisperDecodeOptions decodeOptions,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(_message);
        }
    }
}
