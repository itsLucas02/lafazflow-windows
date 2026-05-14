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
            @"C:\Audio\sample");

        Assert.Contains("-m \"C:\\Models\\ggml-base.en.bin\"", args);
        Assert.Contains("-f \"C:\\Audio\\sample.wav\"", args);
        Assert.Contains("-otxt", args);
        Assert.Contains("-of \"C:\\Audio\\sample\"", args);
    }

    [Fact]
    public void ValidatePathsRejectsMissingCli()
    {
        var modelPath = Path.GetTempFileName();
        try
        {
            var error = WhisperCliTranscriptionService.ValidatePaths(
                @"C:\missing\whisper-cli.exe",
                modelPath);

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
                @"C:\missing\ggml-base.en.bin");

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
        var result = WhisperCliTranscriptionService.CleanTranscript("  Hello LafazFlow.\r\n");

        Assert.Equal("Hello LafazFlow.", result);
    }
}
