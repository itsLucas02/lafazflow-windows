using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperPromptBuilderTests
{
    [Fact]
    public void BuildVocabularyPromptDoesNotDuplicateBuiltInVocabulary()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = ""
        });

        Assert.Equal(AppSettings.DefaultWhisperInitialPrompt, prompt);
        Assert.DoesNotContain("Custom vocabulary:", prompt);
    }

    [Fact]
    public void BuildVocabularyPromptAppendsTrimmedCustomTerms()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = """
                PDPA
                Care Visit
                align
                inline alert
                """
        });

        Assert.Contains("PDPA, Care Visit, align, inline alert.", prompt);
    }

    [Fact]
    public void BuildVocabularyPromptDeduplicatesCaseInsensitivelyAndPreservesFirstCasing()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = """
                PDPA
                pdpa
                Align
                align
                """
        });

        Assert.Contains("PDPA, Align.", prompt);
        Assert.DoesNotContain("pdpa", prompt);
        Assert.DoesNotContain("align.", prompt);
    }

    [Fact]
    public void BuildVocabularyPromptDoesNotAppendCustomTermAlreadyInBasePrompt()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = """
                Supabase
                supabase
                PDPA
                """
        });

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(prompt, "Supabase", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Assert.EndsWith("Custom vocabulary: PDPA.", prompt);
    }
}
