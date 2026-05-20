using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperPromptBuilderTests
{
    [Fact]
    public void BuildVocabularyPromptKeepsBuiltInPromptWhenCustomTermsAreEmpty()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = ""
        });

        Assert.Equal(AppSettings.DefaultWhisperInitialPrompt, prompt);
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

        Assert.Contains("Supabase", prompt);
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
}
