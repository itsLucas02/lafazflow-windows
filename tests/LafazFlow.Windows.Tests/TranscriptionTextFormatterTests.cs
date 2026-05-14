using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class TranscriptionTextFormatterTests
{
    [Fact]
    public void FormatRemovesTimestampLinesAndCollapsesWhitespace()
    {
        var formatted = TranscriptionTextFormatter.Format("""
            [00:00:00.000 --> 00:00:02.000]   hello   world
            this is lafaz flow
            """);

        Assert.Equal("Hello world this is lafaz flow.", formatted);
    }

    [Fact]
    public void FormatRemovesSpacesBeforePunctuation()
    {
        var formatted = TranscriptionTextFormatter.Format("hello world , this is working !");

        Assert.Equal("Hello world, this is working!", formatted);
    }

    [Fact]
    public void FormatDoesNotAddPunctuationAfterExistingSentenceMark()
    {
        var formatted = TranscriptionTextFormatter.Format("hello world?");

        Assert.Equal("Hello world?", formatted);
    }
}
