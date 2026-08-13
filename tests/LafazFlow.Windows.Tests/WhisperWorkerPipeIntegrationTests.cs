using System.Text;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperWorkerPipeIntegrationTests
{
    private static readonly string WorkerPath =
        @"C:\Tools\lafazflow-whisper-worker\bin\lafazflow-whisper-worker.exe";
    private static readonly string ModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin";
    private static readonly string VadModelPath = @"C:\Models\whisper\ggml-silero-v5.1.2.bin";
    private static readonly string FixturesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LafazFlow",
        "Benchmarks",
        "fixtures-m1-2026-08-13");

    private static bool Available => File.Exists(WorkerPath)
        && File.Exists(ModelPath)
        && Directory.Exists(FixturesDirectory);

    [Fact]
    public async Task RealWorkerInitializesTranscribesCancelsAndShutsDown()
    {
        if (!Available)
        {
            return;
        }

        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            ModelPath = ModelPath,
            QualityModelPath = ModelPath,
            VadModelPath = VadModelPath,
            EnableVad = true,
            WhisperThreads = 16
        };

        var fixtures = Directory.GetFiles(FixturesDirectory, "*.wav")
            .OrderBy(path => new FileInfo(path).Length)
            .ToArray();
        var shortFixture = fixtures[0];
        var longFixture = fixtures[^1];
        var expectedText = Normalize(File.ReadAllText(Path.ChangeExtension(shortFixture, ".txt")));

        using var supervisor = new WhisperWorkerSupervisor(new WhisperWorkerSupervisorOptions
        {
            WorkerExecutablePath = WorkerPath,
            ReadinessTimeout = TimeSpan.FromSeconds(30),
            OperationTimeout = TimeSpan.FromMinutes(2),
            ShutdownTimeout = TimeSpan.FromSeconds(5),
            CaptureWorkerDiagnostics = true,
            WorkerDiagnosticsDirectory = Path.Combine(Path.GetTempPath(), "LafazFlowWorkerDiag")
        });

        var session = await supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        Assert.True(session.IsReady);
        Assert.False(string.IsNullOrWhiteSpace(session.FingerprintHex));

        var (shortPcm, shortSamples) = ReadPcm16kMono(shortFixture);
        var final = await session.TranscribeFinalAsync(shortPcm, shortSamples, CancellationToken.None);
        Assert.Equal(WhisperPipeStatus.Ok, final.Status);
        Assert.Equal(expectedText, Normalize(Encoding.UTF8.GetString(final.Data)));

        var health = await session.HealthAsync(CancellationToken.None);
        Assert.Contains("completed=1", health);
        Assert.Contains("backend=cuda", health);

        var (longPcm, longSamples) = ReadPcm16kMono(longFixture);
        var cancelTarget = session.TranscribeFinalAsync(longPcm, longSamples, CancellationToken.None);
        await Task.Delay(150);
        await session.CancelAsync(Guid.NewGuid(), CancellationToken.None);
        var cancelled = await cancelTarget;
        Assert.Equal(WhisperPipeStatus.Aborted, cancelled.Status);

        var after = await session.TranscribeFinalAsync(shortPcm, shortSamples, CancellationToken.None);
        Assert.Equal(WhisperPipeStatus.Ok, after.Status);

        var (previewPcm, previewSamples) = ReadPcm16kMono(longFixture);
        var previewTask = session.TranscribePreviewAsync(previewPcm, previewSamples, CancellationToken.None);
        await Task.Delay(120);
        var priorityFinal = await session.TranscribeFinalAsync(shortPcm, shortSamples, CancellationToken.None);
        Assert.Equal(WhisperPipeStatus.Ok, priorityFinal.Status);
        var previewResult = await previewTask;
        Assert.Equal(WhisperPipeStatus.Aborted, previewResult.Status);

        var crashedProcessId = session.ProcessId;
        using (var crashedProcess = System.Diagnostics.Process.GetProcessById(crashedProcessId))
        {
            crashedProcess.Kill();
        }

        await Task.Delay(300);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            session.TranscribeFinalAsync(shortPcm, shortSamples, CancellationToken.None));

        var restarted = await supervisor.RestartSessionAsync(settings, CancellationToken.None);
        var recovered = await restarted.TranscribeFinalAsync(shortPcm, shortSamples, CancellationToken.None);
        Assert.Equal(WhisperPipeStatus.Ok, recovered.Status);

        var processId = session.ProcessId;
        await supervisor.ShutdownAsync();
        Assert.False(IsProcessRunning(processId));
    }

    [Fact]
    public async Task WorkerExitsWhenClientDisconnectsWithoutShutdownRequest()
    {
        if (!Available)
        {
            return;
        }

        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            ModelPath = ModelPath,
            QualityModelPath = ModelPath,
            VadModelPath = VadModelPath,
            EnableVad = true,
            WhisperThreads = 16
        };

        var supervisor = new WhisperWorkerSupervisor(new WhisperWorkerSupervisorOptions
        {
            WorkerExecutablePath = WorkerPath,
            ReadinessTimeout = TimeSpan.FromSeconds(90),
            OperationTimeout = TimeSpan.FromMinutes(2),
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        });
        var session = await supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        var processId = session.ProcessId;

        // Closing the pipe without an explicit shutdown request simulates the
        // WPF app being killed: the worker must detect the broken pipe and exit
        // instead of lingering as an orphan holding the loaded model.
        supervisor.Dispose();

        await WaitUntilAsync(() => !IsProcessRunning(processId), TimeSpan.FromSeconds(10));
        Assert.False(IsProcessRunning(processId));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private static string Normalize(string text)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"[^\p{L}\p{N}\s]",
            string.Empty);
        return string.Join(
            ' ',
            cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => word.ToLowerInvariant()));
    }

    private static (byte[] Pcm, uint Samples) ReadPcm16kMono(string wavPath)
    {
        var bytes = File.ReadAllBytes(wavPath);
        var position = 12;
        uint dataSize = 0;
        var found = false;
        while (position + 8 <= bytes.Length)
        {
            var id = Encoding.ASCII.GetString(bytes, position, 4);
            var size = BitConverter.ToInt32(bytes, position + 4);
            if (id == "data")
            {
                dataSize = (uint)size;
                found = true;
                break;
            }

            position += 8 + size + (size % 2);
        }

        if (!found)
        {
            throw new InvalidOperationException("No data chunk in fixture.");
        }

        var pcm = new byte[dataSize];
        Array.Copy(bytes, position + 8, pcm, 0, dataSize);
        return (pcm, dataSize / 2);
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
