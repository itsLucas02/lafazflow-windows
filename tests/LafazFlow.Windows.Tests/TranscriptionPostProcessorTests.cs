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
            ["raw_cleanup", "vocabulary", "target_context", "trailing_separator"],
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
}
