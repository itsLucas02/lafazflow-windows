using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class RuntimeDiagnosticsServiceTests
{
    [Fact]
    public void BuildProfileStatusShowsFastCpuAndModelFileName()
    {
        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Fast,
            WhisperBackend = WhisperBackend.Cpu,
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin"
        };
        var service = new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe());

        var status = service.BuildProfileStatus(settings);

        Assert.Equal("Fast / CPU / ggml-base.en.bin", status);
    }

    [Fact]
    public void BuildProfileStatusShowsQualityCudaAndModelFileName()
    {
        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            QualityModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin"
        };
        var service = new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe());

        var status = service.BuildProfileStatus(settings);

        Assert.Equal("Quality / CUDA / ggml-large-v3-turbo-q5_0.bin", status);
    }

    [Fact]
    public void BuildDiagnosticsReportsMissingActiveRuntimeFiles()
    {
        var settings = AppSettings.Default with
        {
            WhisperCliPath = @"C:\missing\whisper-cli.exe",
            ModelPath = @"C:\missing\ggml-base.en.bin"
        };
        var service = new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe
        {
            ExistingFiles = [],
            MicrophoneDeviceCount = 1,
            WritableDirectories = [@"C:\Logs"]
        });

        var rows = service.BuildDiagnostics(settings, @"C:\Logs");

        Assert.Contains(rows, row => row.Name == "Whisper CLI" && row.Severity == RuntimeDiagnosticSeverity.Error);
        Assert.Contains(rows, row => row.Name == "Whisper model" && row.Severity == RuntimeDiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildDiagnosticsReportsCudaAndVadFilesWhenEnabled()
    {
        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            CudaWhisperCliPath = @"C:\missing\cuda\whisper-cli.exe",
            QualityModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            EnableVad = true,
            VadModelPath = @"C:\missing\ggml-silero-v5.1.2.bin"
        };
        var service = new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe
        {
            ExistingFiles = [settings.QualityModelPath],
            MicrophoneDeviceCount = 1,
            WritableDirectories = [@"C:\Logs"]
        });

        var rows = service.BuildDiagnostics(settings, @"C:\Logs");

        Assert.Contains(rows, row => row.Name == "CUDA Whisper CLI" && row.Severity == RuntimeDiagnosticSeverity.Error);
        Assert.Contains(rows, row => row.Name == "VAD model" && row.Severity == RuntimeDiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildDiagnosticsReportsMicrophoneAndLogsFolderStatus()
    {
        var settings = AppSettings.Default with
        {
            WhisperCliPath = @"C:\Tools\whisper-cli.exe",
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin"
        };
        var service = new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe
        {
            ExistingFiles = [settings.WhisperCliPath, settings.ModelPath],
            MicrophoneDeviceCount = 0,
            WritableDirectories = []
        });

        var rows = service.BuildDiagnostics(settings, @"C:\Logs");

        Assert.Contains(rows, row => row.Name == "Microphone" && row.Severity == RuntimeDiagnosticSeverity.Error);
        Assert.Contains(rows, row => row.Name == "Logs folder" && row.Severity == RuntimeDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task TestTranscriptionSmokeReportsCliLaunchFailure()
    {
        var settings = AppSettings.Default with
        {
            WhisperCliPath = @"C:\Tools\whisper-cli.exe",
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin"
        };
        var probe = new FakeRuntimeEnvironmentProbe
        {
            ExistingFiles = [settings.WhisperCliPath, settings.ModelPath],
            SmokeResult = new RuntimeSmokeCheckResult(false, "cublas64_13.dll was not found")
        };
        var service = new RuntimeDiagnosticsService(probe);

        var row = await service.TestTranscriptionSmokeAsync(settings, CancellationToken.None);

        Assert.Equal("Test transcription", row.Name);
        Assert.Equal(RuntimeDiagnosticSeverity.Error, row.Severity);
        Assert.Contains("cublas64_13.dll", row.Detail);
        Assert.Equal(settings.WhisperCliPath, probe.LastSmokeCliPath);
    }

    private sealed class FakeRuntimeEnvironmentProbe : IRuntimeEnvironmentProbe
    {
        public HashSet<string> ExistingFiles { get; init; } = [];
        public HashSet<string> WritableDirectories { get; init; } = [];
        public int MicrophoneDeviceCount { get; init; } = 1;
        public RuntimeSmokeCheckResult SmokeResult { get; init; } = new(true, "Whisper CLI started.");
        public string LastSmokeCliPath { get; private set; } = "";

        public bool FileExists(string path) => ExistingFiles.Contains(path);

        public int GetMicrophoneDeviceCount() => MicrophoneDeviceCount;

        public bool CanWriteToDirectory(string path) => WritableDirectories.Contains(path);

        public Task<RuntimeSmokeCheckResult> RunWhisperSmokeCheckAsync(
            string whisperCliPath,
            string processPath,
            CancellationToken cancellationToken)
        {
            LastSmokeCliPath = whisperCliPath;
            return Task.FromResult(SmokeResult);
        }
    }
}
