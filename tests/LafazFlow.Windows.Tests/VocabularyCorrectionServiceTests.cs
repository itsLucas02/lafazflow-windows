using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class VocabularyCorrectionServiceTests
{
    [Fact]
    public void ApplyDefaultsFixesTechnicalTerms()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(
            "Testing Super B's, Vircell, Tail, skill, netlify, and mintlify.");

        Assert.Equal("Testing Supabase, Vercel, Tailscale, Netlify, and Mintlify.", corrected);
    }

    [Fact]
    public void ApplyDefaultsPreservesUnrelatedText()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Testing one, two, three.");

        Assert.Equal("Testing one, two, three.", corrected);
    }
}
