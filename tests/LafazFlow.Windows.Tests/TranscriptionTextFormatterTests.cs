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

    [Theory]
    [InlineData(
        "It's broken. It's fucking broken. Over and over again. And there is nothing that you can do to fix it.",
        "It's broken. It's fucking broken. Over and over again, and there is nothing that you can do to fix it.")]
    [InlineData(
        "Document everything and then make a checklist. And then we go one by one until everything completes.",
        "Document everything and then make a checklist, and then we go one by one until everything completes.")]
    public void FormatRepairsHighConfidenceAndContinuationBreaks(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("So what do you suggest us to do then.", "So what do you suggest us to do then?")]
    [InlineData("But how do we manage them.", "But how do we manage them?")]
    [InlineData("Should we do some sort of a roadmap for this.", "Should we do some sort of a roadmap for this?")]
    public void FormatMarksConversationalQuestionLeadIns(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData(
        "This is complete. And the next section starts here.",
        "This is complete. And the next section starts here.")]
    [InlineData(
        "This is complete. Andrew will review it.",
        "This is complete. Andrew will review it.")]
    public void FormatDoesNotAggressivelyMergeNormalSentences(string input, string expected)
    {
        var formatted = TranscriptionTextFormatter.Format(input);

        Assert.Equal(expected, formatted);
    }
}
