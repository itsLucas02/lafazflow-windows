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
    [InlineData("My name is Lukamine.", "My name is Luqman.")]
    [InlineData("My name is Lukman.", "My name is Luqman.")]
    [InlineData("My name is Luqmen.", "My name is Luqman.")]
    [InlineData("My name is L-U-Q-M-A-N.", "My name is Luqman.")]
    [InlineData("My name is S-N-L-U-Q-M-E-N.", "My name is Luqman.")]
    public void ApplyDefaultsFixesLuqmanVariants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
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
        var corrected = VocabularyCorrectionService.ApplyDefaults("LafazFlow rapidness, not repeteness.");

        Assert.Equal("LafazFlow rapidness, not rapidness.", corrected);
    }

    [Theory]
    [InlineData("Run git comit now.", "Run git commit now.")]
    [InlineData("Run git come in now.", "Run git commit now.")]
    [InlineData("Please come in and push.", "Please commit and push.")]
    [InlineData("It comes in and push.", "It commit and push.")]
    public void ApplyDefaultsFixesCommitCodingVariants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Fact]
    public void ApplyDefaultsPreservesNormalComeInSentence()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Please come in when you are ready.");

        Assert.Equal("Please come in when you are ready.", corrected);
    }

    [Theory]
    [InlineData("Use Chat CN components.", "Use shadcn components.")]
    [InlineData("Install ChatCN UI.", "Install shadcn UI.")]
    [InlineData("Open shad cn docs.", "Open shadcn docs.")]
    [InlineData("Use Chet's the end components.", "Use shadcn components.")]
    [InlineData("Install Shut CN UI.", "Install shadcn UI.")]
    [InlineData("Open Sh*t's the end docs.", "Open shadcn docs.")]
    [InlineData("Install Shit, CN UI.", "Install shadcn UI.")]
    [InlineData("Open Shut the end docs.", "Open shadcn docs.")]
    [InlineData("Use Sh*t-C-N components.", "Use shadcn components.")]
    [InlineData("Install Shut-see-in UI.", "Install shadcn UI.")]
    [InlineData("Use Shat-C-N components.", "Use shadcn components.")]
    [InlineData("Open Shetxian docs.", "Open shadcn docs.")]
    public void ApplyDefaultsFixesShadcnVariants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Fact]
    public void ApplyDefaultsCleansDeveloperDictationExample()
    {
        var input = "We can take the placeholder name for a temporary branding name for now, since we haven't yet finalized on the branding name, and therefore I'm choosing Care Visit. We also need to make sure that our UI UX that are using shadcn is standardized and doesn't have variations, meaning that, you know, instead of importing multiple things, multiple methods or multiple variations for just one simple UI components, perhaps we can reuse, you know, reuse whatever we use have. Install one's reuse forever. you can see this from shadcn skills $shadcn-ui and $build-web-apps:shadcn . Go to those skills I mentioned and then tell me what do you think? Everything is documented in those skills documentation.";

        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal("We can take the placeholder name for a temporary branding name for now, since we haven't yet finalized on the branding name, and therefore I'm choosing Care Visit. We also need to make sure that our UI UX that are using shadcn is standardized and doesn't have variations, meaning that, you know, instead of importing multiple things, multiple methods or multiple variations for just one simple UI components, perhaps we can reuse, you know, reuse whatever we have. Install once, reuse forever. you can see this from shadcn skills $shadcn-ui and $build-web-apps:shadcn. Go to those skills I mentioned and then tell me what do you think. Everything is documented in those skills documentation.", corrected);
    }

    [Fact]
    public void ApplyDefaultsPreservesUnrelatedText()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Testing one, two, three.");

        Assert.Equal("Testing one, two, three.", corrected);
    }
}
