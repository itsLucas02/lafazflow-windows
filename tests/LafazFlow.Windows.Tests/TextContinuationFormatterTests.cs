using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class TextContinuationFormatterTests
{
    [Theory]
    [InlineData("Whatever,", "Hello, over.", "hello, over.")]
    [InlineData("Whatever: ", "Hello, over.", "hello, over.")]
    [InlineData("Whatever; ", "Hello, over.", "hello, over.")]
    public void ApplyTargetContextLowercasesContinuationAfterMidSentencePunctuation(
        string textBeforeCaret,
        string transcript,
        string expected)
    {
        var formatted = TextContinuationFormatter.ApplyTargetContext(transcript, textBeforeCaret);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("", "Hello, over.")]
    [InlineData("Whatever. ", "Hello, over.")]
    [InlineData("Whatever? ", "Hello, over.")]
    [InlineData("Whatever! ", "Hello, over.")]
    public void ApplyTargetContextKeepsSentenceStartWhenContextIsUnavailableOrComplete(
        string textBeforeCaret,
        string transcript)
    {
        var formatted = TextContinuationFormatter.ApplyTargetContext(transcript, textBeforeCaret);

        Assert.Equal(transcript, formatted);
    }

    [Theory]
    [InlineData("Whatever,", "API endpoint is ready.")]
    [InlineData("Whatever,", "I think so.")]
    public void ApplyTargetContextPreservesAcronymsAndPronounI(string textBeforeCaret, string transcript)
    {
        var formatted = TextContinuationFormatter.ApplyTargetContext(transcript, textBeforeCaret);

        Assert.Equal(transcript, formatted);
    }
}
