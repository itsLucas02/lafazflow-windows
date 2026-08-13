using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperTimingParserTests
{
    [Fact]
    public void ParsesWhisperPrintTimingsBlock()
    {
        const string output = """
            whisper_init_from_file_with_params_no_state: loading model
            whisper_model_load: loading model from 'C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin'
            whisper_print_timings:        load time =   891.00 ms
            whisper_print_timings:     fallbacks =   0 p /   0 h
            whisper_print_timings:     sample time =    14.00 ms
            whisper_print_timings:      encode time =  1211.00 ms / 1 runs
            whisper_print_timings:      decode time =   234.00 ms / 7 runs
            whisper_print_timings:      prompt time =   331.00 ms / 213 tokens
            whisper_print_timings:     total time =  1892.00 ms / 226 tokens
            """;

        var timing = WhisperTimingParser.Parse(output);

        Assert.Equal(891, timing.LoadMs);
        Assert.Equal(14, timing.SampleMs);
        Assert.Equal(1211, timing.EncodeMs);
        Assert.Equal(234, timing.DecodeMs);
        Assert.Equal(331, timing.PromptMs);
        Assert.Equal(1892, timing.TotalMs);
        Assert.Equal(226, timing.TokenCount);
        Assert.Equal(0, timing.FallbackCount);
    }

    [Fact]
    public void ReturnsEmptyTimingForMissingOrBlankOutput()
    {
        Assert.Equal(WhisperTimingParser.Empty, WhisperTimingParser.Parse(""));
        Assert.Equal(WhisperTimingParser.Empty, WhisperTimingParser.Parse("plain text without timing lines"));
    }

    [Fact]
    public void ReturnsPartialTimingWhenOnlySomeLinesExist()
    {
        const string output = """
            whisper_print_timings:        load time =   100.00 ms
            whisper_print_timings:     total time =   500.00 ms / 40 tokens
            """;

        var timing = WhisperTimingParser.Parse(output);

        Assert.Equal(100, timing.LoadMs);
        Assert.Null(timing.EncodeMs);
        Assert.Equal(500, timing.TotalMs);
        Assert.Equal(40, timing.TokenCount);
    }
}
