using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperDecodeOptionsTests
{
    [Fact]
    public void CompleteFinalPassDisablesVadWithoutChangingQualitySettings()
    {
        var original = WhisperDecodeOptions.QualityWithVad("vad-model.bin") with
        {
            Temperature = 0.2,
            NoFallback = false,
            MaxContextTokens = 64
        };

        var final = original.ForCompleteFinalPass();

        Assert.False(final.EnableVad);
        Assert.Equal("", final.VadModelPath);
        Assert.Equal(original.Temperature, final.Temperature);
        Assert.Equal(original.NoFallback, final.NoFallback);
        Assert.Equal(original.SuppressNonSpeechTokens, final.SuppressNonSpeechTokens);
        Assert.Equal(original.MaxContextTokens, final.MaxContextTokens);
        Assert.True(original.EnableVad);
        Assert.Equal("vad-model.bin", original.VadModelPath);
    }
}
