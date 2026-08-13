using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperPromptBuilderTests
{
    [Fact]
    public void BuildVocabularyPromptIncludesDefaultVocabularyWhenCustomTermsAreEmpty()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = ""
        });

        Assert.StartsWith(AppSettings.DefaultWhisperInitialPrompt, prompt);
        Assert.Contains("DeepSeek", prompt);
        Assert.Contains("Supabase", prompt);
        Assert.Contains("MediBrave", prompt);
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

        Assert.Contains("DeepSeek", prompt);
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

        Assert.Contains("DeepSeek", prompt);
        Assert.Contains("PDPA, Align.", prompt);
        Assert.DoesNotContain("pdpa", prompt);
        Assert.DoesNotContain("align.", prompt);
    }

    [Fact]
    public void BuildVocabularyPromptDeduplicatesCustomTermThatMatchesDefault()
    {
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(AppSettings.Default with
        {
            CustomVocabularyTerms = """
                deepseek
                Supabase
                PDPA
                """
        });

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(prompt, "DeepSeek"));
        Assert.Contains("LafazFlow, PDPA.", prompt);
    }
}
