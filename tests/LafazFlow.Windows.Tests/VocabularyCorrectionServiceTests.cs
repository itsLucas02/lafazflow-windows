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

    [Fact]
    public void ApplyDefaultsPreservesUnrelatedText()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Testing one, two, three.");

        Assert.Equal("Testing one, two, three.", corrected);
    }
}
