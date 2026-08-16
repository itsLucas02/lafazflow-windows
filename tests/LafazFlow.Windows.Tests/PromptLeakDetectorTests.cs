using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class PromptLeakDetectorTests
{
    private const string Prompt =
        "Supabase, Contabo, Vercel. Custom vocabulary: DeepSeek, Supabase, MediBrave, Luqman.";

    [Fact]
    public void DetectsObservedPromptLeakWithRepeatedHallucinatedWord()
    {
        var leaked = string.Join(
            " ",
            new[]
            {
                "Custom vocabulary,",
                "Individu, Individu, Individu, Individu, Individu, Individu, Individu, Individu,",
                "Individu, Individu, Individu, Individu, Individu, Individu, Individu, Individu."
            });

        Assert.True(PromptLeakDetector.IsPromptLeak(leaked, Prompt));
    }

    [Fact]
    public void DetectsVerbatimPromptEchoOfSignificantLength()
    {
        var echo = "Custom vocabulary: DeepSeek, Supabase, MediBrave, Luqman.";

        Assert.True(PromptLeakDetector.IsPromptLeak(echo, Prompt));
    }

    [Fact]
    public void DoesNotFlagLegitimateCustomVocabularyDictation()
    {
        const string legitimate = "Custom vocabulary is important, please update the list.";

        Assert.False(PromptLeakDetector.IsPromptLeak(legitimate, Prompt));
    }

    [Fact]
    public void DoesNotFlagShortMarkerOnly()
    {
        Assert.False(PromptLeakDetector.IsPromptLeak("Custom vocabulary.", Prompt));
    }

    [Fact]
    public void DoesNotFlagNormalDictation()
    {
        Assert.False(
            PromptLeakDetector.IsPromptLeak(
                "The quick brown fox jumps over the lazy dog and when the sun sets we all go home.",
                Prompt));
    }

    [Fact]
    public void DoesNotFlagRepetitionBelowThresholdWithoutMarker()
    {
        var repeated = string.Join(" ", Enumerable.Repeat("Individu", 7)) + ".";

        Assert.False(PromptLeakDetector.IsPromptLeak(repeated, Prompt));
    }

    [Fact]
    public void RepetitionBelowThresholdWithMarkerIsNotFlagged()
    {
        var text = "Custom vocabulary, " + string.Join(" ", Enumerable.Repeat("Individu", 7)) + ".";

        Assert.False(PromptLeakDetector.IsPromptLeak(text, Prompt));
    }

    [Fact]
    public void MatchingIsCaseInsensitiveAndPunctuationInsensitive()
    {
        var leaked = "CUSTOM VOCABULARY, individu, individu, individu, individu, individu, individu, individu, individu.";

        Assert.True(PromptLeakDetector.IsPromptLeak(leaked, Prompt));
    }

    [Fact]
    public void EmptyTranscriptOrPromptIsNeverALeak()
    {
        Assert.False(PromptLeakDetector.IsPromptLeak("", Prompt));
        Assert.False(PromptLeakDetector.IsPromptLeak("hello world", ""));
    }

    [Fact]
    public void DetectsPureRepetitionLoopWithoutPromptMarker()
    {
        var leaked = string.Join(".", Enumerable.Repeat("1", 16)) + ".";

        Assert.True(PromptLeakDetector.IsPromptLeak(leaked, Prompt));
    }

    [Fact]
    public void DoesNotFlagRepetitionEmbeddedInRealSpeech()
    {
        var text = string.Join(" ", Enumerable.Repeat("no", 14))
            + " I will not accept this change, please revise the plan.";

        Assert.False(PromptLeakDetector.IsPromptLeak(text, Prompt));
    }

    [Fact]
    public void DoesNotFlagShortRepeatedWordInRealSpeech()
    {
        var text = string.Join(" ", Enumerable.Repeat("very", 6)) + " good point, let us proceed.";

        Assert.False(PromptLeakDetector.IsPromptLeak(text, Prompt));
    }
}
