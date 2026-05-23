using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperCliTranscriptionServiceTests
{
    [Fact]
    public void BuildArgumentsUsesModelInputAndTxtOutput()
    {
        var args = WhisperCliTranscriptionService.BuildArguments(
            @"C:\Models\ggml-base.en.bin",
            @"C:\Audio\sample.wav",
            @"C:\Audio\sample",
            "",
            16);

        Assert.Contains("-m \"C:\\Models\\ggml-base.en.bin\"", args);
        Assert.Contains("-f \"C:\\Audio\\sample.wav\"", args);
        Assert.Contains("-t 16", args);
        Assert.Contains("-otxt", args);
        Assert.Contains("-nt", args);
        Assert.Contains("-tp 0", args);
        Assert.Contains("-nf", args);
        Assert.Contains("-of \"C:\\Audio\\sample\"", args);
    }

    [Fact]
    public void BuildArgumentsIncludesPromptWhenConfigured()
    {
        var args = WhisperCliTranscriptionService.BuildArguments(
            @"C:\Models\ggml-large-v3-turbo.bin",
            @"C:\Audio\sample.wav",
            @"C:\Audio\sample",
            "Supabase Vercel Tailscale \"quoted\"",
            16);

        Assert.Contains("English dictation only.", args);
        Assert.Contains("Do not translate into Malay or Indonesian.", args);
        Assert.Contains("Supabase Vercel Tailscale \\\"quoted\\\"", args);
        Assert.Contains("--carry-initial-prompt", args);
    }

    [Fact]
    public void BuildArgumentsUsesReferenceQualityDecodeSettingsForQualityProfile()
    {
        var args = WhisperCliTranscriptionService.BuildArguments(
            @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            @"C:\Audio\sample.wav",
            @"C:\Audio\sample",
            "Supabase",
            16,
            WhisperDecodeOptions.QualityWithVad(@"C:\Models\whisper\ggml-silero-v5.1.2.bin"));

        Assert.Contains("-l en", args);
        Assert.Contains("-tp 0", args);
        Assert.Contains("-nf", args);
        Assert.Contains("-sns", args);
        Assert.Contains("--vad", args);
        Assert.Contains("-vm \"C:\\Models\\whisper\\ggml-silero-v5.1.2.bin\"", args);
        Assert.Contains("-vt 0.50", args);
        Assert.Contains("-vspd 250", args);
        Assert.Contains("-vsd 100", args);
        Assert.Contains("-vp 30", args);
        Assert.Contains("-vo 0.10", args);
    }

    [Fact]
    public void BuildArgumentsUsesMacOsLikeDecodeSettings()
    {
        var args = WhisperCliTranscriptionService.BuildArguments(
            @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            @"C:\Audio\sample.wav",
            @"C:\Audio\sample",
            "Supabase",
            16,
            WhisperDecodeOptions.MacOsLike);

        Assert.Contains("-l en", args);
        Assert.Contains("-tp 0.2", args);
        Assert.Contains("-mc 0", args);
        Assert.DoesNotContain("-nf", args);
        Assert.DoesNotContain("-sns", args);
        Assert.DoesNotContain("--vad", args);
    }

    [Fact]
    public void ResolveRuntimeUsesCudaQualityProfileWhenConfigured()
    {
        var settings = AppSettings.Default with
        {
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            WhisperCliPath = @"C:\Tools\whisper.cpp\Release\whisper-cli.exe",
            CudaWhisperCliPath = @"C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe",
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin",
            QualityModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            EnableVad = true,
            VadModelPath = @"C:\Models\whisper\ggml-silero-v5.1.2.bin"
        };

        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);

        Assert.Equal(@"C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe", runtime.CliPath);
        Assert.Equal(@"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin", runtime.ModelPath);
        Assert.Equal(0, runtime.DecodeOptions.Temperature);
        Assert.True(runtime.DecodeOptions.NoFallback);
        Assert.True(runtime.DecodeOptions.EnableVad);
    }

    [Fact]
    public void BuildProcessPathPrependsWhisperAndCudaRuntimeDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliDirectory = Directory.CreateDirectory(Path.Combine(root, "whisper", "bin")).FullName;
        var cudaDirectory = Directory.CreateDirectory(Path.Combine(root, "cuda", "bin", "x64")).FullName;
        var existingDirectory = Directory.CreateDirectory(Path.Combine(root, "existing")).FullName;
        var cliPath = Path.Combine(cliDirectory, "whisper-cli.exe");

        var path = WhisperCliTranscriptionService.BuildProcessPath(
            cliPath,
            existingDirectory,
            [cudaDirectory]);

        var entries = path.Split(Path.PathSeparator);
        Assert.Equal(cliDirectory, entries[0]);
        Assert.Equal(cudaDirectory, entries[1]);
        Assert.Equal(existingDirectory, entries[2]);
        Assert.Equal(3, entries.Length);
    }

    [Fact]
    public void ValidatePathsRejectsMissingCli()
    {
        var modelPath = Path.GetTempFileName();
        try
        {
            var error = WhisperCliTranscriptionService.ValidatePaths(
                @"C:\missing\whisper-cli.exe",
                modelPath,
                WhisperDecodeOptions.Fast);

            Assert.Equal("Whisper CLI was not found.", error);
        }
        finally
        {
            File.Delete(modelPath);
        }
    }

    [Fact]
    public void ValidatePathsRejectsMissingModel()
    {
        var cliPath = Path.GetTempFileName();
        try
        {
            var error = WhisperCliTranscriptionService.ValidatePaths(
                cliPath,
                @"C:\missing\ggml-base.en.bin",
                WhisperDecodeOptions.Fast);

            Assert.Equal("Whisper model was not found.", error);
        }
        finally
        {
            File.Delete(cliPath);
        }
    }

    [Fact]
    public void CleanTranscriptTrimsWhitespace()
    {
        var result = WhisperCliTranscriptionService.CleanTranscript("  hello LafazFlow\r\n");

        Assert.Equal("Hello LafazFlow.", result);
    }
}
