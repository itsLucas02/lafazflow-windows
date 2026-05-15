using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class VocabularyCorrectionServiceTests
{
    [Fact]
    public void ApplyDefaultsFixesTechnicalTerms()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(
            "Testing Super B's, Vircell, Tail, skill, netlify, mintlify, and Maddy Breath.");

        Assert.Equal("Testing Supabase, Vercel, Tailscale, Netlify, Mintlify, and MediBrave.", corrected);
    }

    [Theory]
    [InlineData("voice ink")]
    [InlineData("voicing")]
    [InlineData("voice in")]
    public void ApplyDefaultsFixesVoiceInkVariants(string variant)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults($"Open {variant}.");

        Assert.Equal("Open VoiceInk.", corrected);
    }

    [Theory]
    [InlineData("medibrief")]
    [InlineData("Mad brave")]
    [InlineData("medi brave")]
    [InlineData("maddy brave")]
    [InlineData("Maddy Breath")]
    public void ApplyDefaultsFixesMediBraveVariants(string variant)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults($"Open {variant}.");

        Assert.Equal("Open MediBrave.", corrected);
    }

    [Theory]
    [InlineData("That's 1-2-3 over.", "Test 1-2-3 over.")]
    [InlineData("That's one, two, three, over.", "Test one, two, three, over.")]
    [InlineData("That's, that's, that's.", "Test, test, test.")]
    public void ApplyDefaultsFixesTestingDictationThatsVariants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Fact]
    public void ApplyDefaultsPreservesNormalThatsSentences()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("That's correct, and that's okay.");

        Assert.Equal("That's correct, and that's okay.", corrected);
    }

    [Fact]
    public void ApplyDefaultsFixesRapidnessMisrecognition()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("VoiceInk rapidness, not repeteness.");

        Assert.Equal("VoiceInk rapidness, not rapidness.", corrected);
    }

    [Fact]
    public void ApplyDefaultsPreservesUnrelatedText()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Testing one, two, three.");

        Assert.Equal("Testing one, two, three.", corrected);
    }
}
