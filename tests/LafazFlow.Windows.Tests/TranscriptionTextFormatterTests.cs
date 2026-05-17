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

    [Theory]
    [InlineData(
        "why is it not marking the previous sentence as a question sentence",
        "Why is it not marking the previous sentence as a question sentence?")]
    [InlineData(
        "what model are we using right now",
        "What model are we using right now?")]
    [InlineData(
        "can you tell me your name",
        "Can you tell me your name?")]
    public void FormatAddsQuestionMarkForClearQuestionStarters(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData(
        "wait. why is it not marking the previous sentence as a question sentence",
        "Wait, why is it not marking the previous sentence as a question sentence?")]
    [InlineData(
        "wait. what model are we using right now",
        "Wait, what model are we using right now?")]
    [InlineData(
        "wait. how does this work",
        "Wait, how does this work?")]
    public void FormatUsesCommaForWaitQuestionLeadIn(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void FormatPreservesNonQuestionWaitSentence()
    {
        var formatted = TranscriptionTextFormatter.Format("wait here for a moment");

        Assert.Equal("Wait here for a moment.", formatted);
    }

    [Theory]
    [InlineData("hello world [BLANK_AUDIO].", "Hello world.")]
    [InlineData("hello world [BLANK_AUDIO]", "Hello world.")]
    [InlineData("[BLANK_AUDIO] hello world", "Hello world.")]
    [InlineData("hello [blank audio] world", "Hello world.")]
    [InlineData("hello [BLANK_AUDIO] [BLANK_AUDIO].", "Hello.")]
    public void FormatRemovesBlankAudioMarkers(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("hello world [MUSIC PLAYING].", "Hello world.")]
    [InlineData("[Music playing] hello world", "Hello world.")]
    [InlineData("hello [LAUGHTER] world", "Hello world.")]
    [InlineData("keep [important note] here", "Keep [important note] here.")]
    public void FormatRemovesKnownNonSpeechMarkersOnly(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }
}
