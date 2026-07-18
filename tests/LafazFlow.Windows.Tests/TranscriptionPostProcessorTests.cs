using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class TranscriptionPostProcessorTests
{
    private readonly TranscriptionPostProcessor _processor = new();

    [Fact]
    public void ProcessRunsStagesInExpectedOrder()
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            "Open superbiz.",
            AppSettings.Default,
            ""));

        Assert.Equal(
            ["raw_cleanup", "vocabulary", "developer_literal_formatting", "target_context", "trailing_separator"],
            result.Stages.Select(stage => stage.Stage));
    }

    [Fact]
    public void ProcessAppliesVocabularyTargetContextAndTrailingSeparator()
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            "Open superbiz.",
            AppSettings.Default,
            "Whatever,"));

        Assert.Equal("open Supabase. ", result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "vocabulary", Changed: true });
        Assert.Contains(result.Stages, stage => stage is { Stage: "developer_literal_formatting", Changed: false });
        Assert.Contains(result.Stages, stage => stage is { Stage: "target_context", Changed: true });
        Assert.Contains(result.Stages, stage => stage is { Stage: "trailing_separator", Changed: true });
    }

    [Fact]
    public void ProcessSkipsVocabularyWhenDisabled()
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            "Open superbiz.",
            AppSettings.Default with { EnableVocabularyCorrections = false },
            ""));

        Assert.Equal("Open superbiz. ", result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "vocabulary", Skipped: true });
    }

    [Fact]
    public void ProcessSkipsTrailingSeparatorWhenDisabled()
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            "Open Supabase.",
            AppSettings.Default with { AppendTrailingSpace = false },
            ""));

        Assert.Equal("Open Supabase.", result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "trailing_separator", Skipped: true });
    }

    [Theory]
    [InlineData("Um, open Supabase.", "Open Supabase. ")]
    [InlineData("uh open Supabase.", "Open Supabase. ")]
    [InlineData("hmm, open Supabase.", "Open Supabase. ")]
    public void ProcessRemovesLowRiskLeadingFillers(string input, string expected)
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            input,
            AppSettings.Default,
            ""));

        Assert.Equal(expected, result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "raw_cleanup", Changed: true });
    }

    [Theory]
    [InlineData("wh wh wh what is happening?", "wh what is happening? ")]
    [InlineData("I I I think this works.", "I think this works. ")]
    public void ProcessCollapsesRepeatedShortWordStutters(string input, string expected)
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            input,
            AppSettings.Default,
            ""));

        Assert.Equal(expected, result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "raw_cleanup", Changed: true });
    }

    [Fact]
    public void ProcessDoesNotCollapseMeaningfulRepeatedLongWords()
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            "test test test.",
            AppSettings.Default,
            ""));

        Assert.Equal("test test test. ", result.Text);
    }

    [Fact]
    public void ProcessKeepsRecentOwnerExamplesRepairedByFormatterAndVocabularyStages()
    {
        var formatted = TranscriptionTextFormatter.Format(
            "fix what we are using currently? So that we don't need to keep fixing each of them individually.");

        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            formatted,
            AppSettings.Default,
            ""));

        Assert.Equal(
            "Fix what we are using currently, so that we don't need to keep fixing each of them individually. ",
            result.Text);
    }

    [Theory]
    [InlineData("slash help", "/help ")]
    [InlineData("forward slash help", "/help ")]
    [InlineData("dot env", ".env ")]
    [InlineData("components dot json", "components.json ")]
    [InlineData("at sign luqman", "@luqman ")]
    [InlineData("backtick npm run dev backtick", "`npm run dev` ")]
    [InlineData("quote hello quote", "\"hello\" ")]
    [InlineData("open paren props close paren", "(props) ")]
    [InlineData("open bracket index close bracket", "[index] ")]
    [InlineData("open brace value close brace", "{value} ")]
    public void ProcessFormatsHighConfidenceDeveloperLiterals(string input, string expected)
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            input,
            AppSettings.Default,
            ""));

        Assert.Equal(expected, result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "developer_literal_formatting", Skipped: false });
    }

    [Fact]
    public void ProcessRecordsDeveloperLiteralFormattingStageChanges()
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            "slash help",
            AppSettings.Default,
            ""));

        Assert.Equal("/help ", result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "developer_literal_formatting", Changed: true });
    }

    [Theory]
    [InlineData("Slash is a punctuation word.")]
    [InlineData("The dot is visible.")]
    [InlineData("Meet me at sign language class.")]
    [InlineData("Quote the documentation carefully.")]
    [InlineData("Open parent company profile.")]
    public void ProcessKeepsNormalEnglishLiteralWordsUnchanged(string input)
    {
        var result = _processor.Process(new TranscriptionPostProcessingRequest(
            input,
            AppSettings.Default,
            ""));

        Assert.Equal($"{input} ", result.Text);
        Assert.Contains(result.Stages, stage => stage is { Stage: "developer_literal_formatting", Changed: false });
    }
}
