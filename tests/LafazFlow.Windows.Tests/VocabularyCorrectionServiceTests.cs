using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class VocabularyCorrectionServiceTests
{
    [Fact]
    public void ApplyDefaultsFixesTechnicalTerms()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(
            "Testing Super B's, superbiz, Vircell, Tail, skill, netlify, mintlify, and Maddy Breath.");

        Assert.Equal("Testing Supabase, Supabase, Vercel, Tailscale, Netlify, Mintlify, and MediBrave.", corrected);
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
    [InlineData("Use contact 7 for current docs.", "Use Context7 for current docs.")]
    [InlineData("Ask contacts 7 for library docs.", "Ask Context7 for library docs.")]
    [InlineData("Open contact seven MCP.", "Open Context7 MCP.")]
    [InlineData("Use contacts seven with AI agents.", "Use Context7 with AI agents.")]
    public void ApplyDefaultsFixesContext7Variants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("Open the M C P docs.", "Open the MCP docs.")]
    [InlineData("Use em c p tools.", "Use MCP tools.")]
    [InlineData("Start the vite app.", "Start the Vite app.")]
    [InlineData("Open Vite JS docs.", "Open Vite docs.")]
    public void ApplyDefaultsFixesAgentAndFrontendToolingVariants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
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

    [Theory]
    [InlineData("Let's think one, two, three, over.", "Testing one, two, three, over.")]
    [InlineData("Let's think 1, 2, 3.", "Testing 1, 2, 3.")]
    [InlineData("Let's think 1,2,3 over.", "Testing 1, 2, 3, over.")]
    [InlineData("Let's think one two three over.", "Testing one two three, over.")]
    public void ApplyDefaultsFixesTestingDictationLetsThinkVariants(string input, string expected)
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
    public void ApplyDefaultsPreservesNormalLetsThinkSentences()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Let's think about this before coding.");

        Assert.Equal("Let's think about this before coding.", corrected);
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

    [Theory]
    [InlineData("Without any rappers.", "Without any wrappers.")]
    [InlineData("Just purely their code without any rappers at all.", "Just purely their code without any wrappers at all.")]
    [InlineData("Use component rappers around the alert.", "Use component wrappers around the alert.")]
    [InlineData("Render it with rappers disabled.", "Render it with wrappers disabled.")]
    [InlineData("Keep it with no rappers.", "Keep it with no wrappers.")]
    public void ApplyDefaultsFixesWrapperDictationInCodingContexts(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("The rappers performed well.")]
    [InlineData("Those are my favorite rappers.")]
    [InlineData("The rappers released an album.")]
    public void ApplyDefaultsPreservesRealRapperContexts(string input)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(input, corrected);
    }

    [Theory]
    [InlineData("I would like to see DRs originally without being wrapped.", "I would like to see theirs originally without being wrapped.")]
    [InlineData("I want to compare DRs rawly.", "I want to compare theirs rawly.")]
    [InlineData("Can we use DRs originally?", "Can we use theirs originally?")]
    [InlineData("What if we just took DRs?", "What if we just took theirs?")]
    public void ApplyDefaultsFixesTheirsDictationDrsInUiComparisonContexts(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("The DRS system is enabled.")]
    [InlineData("Check the DRS score.")]
    [InlineData("Open the DRS file.")]
    public void ApplyDefaultsPreservesDrsAcronymContexts(string input)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(input, corrected);
    }

    [Fact]
    public void ApplyDefaultsCleansDeveloperDictationExample()
    {
        var input = "We can take the placeholder name for a temporary branding name for now, since we haven't yet finalized on the branding name, and therefore I'm choosing Care Visit. We also need to make sure that our UI UX that are using shadcn is standardized and doesn't have variations, meaning that, you know, instead of importing multiple things, multiple methods or multiple variations for just one simple UI components, perhaps we can reuse, you know, reuse whatever we use have. Install one's reuse forever. you can see this from shadcn skills $shadcn-ui and $build-web-apps:shadcn . Go to those skills I mentioned and then tell me what do you think? Everything is documented in those skills documentation.";

        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal("We can take the placeholder name for a temporary branding name for now, since we haven't yet finalized on the branding name, and therefore I'm choosing Care Visit. We also need to make sure that our UI UX that are using shadcn is standardized and doesn't have variations, meaning that, you know, instead of importing multiple things, multiple methods or multiple variations for just one simple UI components, perhaps we can reuse, you know, reuse whatever we have. Install once, reuse forever. you can see this from shadcn skills $shadcn-ui and $build-web-apps:shadcn. Go to those skills I mentioned and then tell me what do you think. Everything is documented in those skills documentation.", corrected);
    }

    [Theory]
    [InlineData("Please write s-t-a-f-f.", "Please write staff.")]
    [InlineData("Please write S T A F F.", "Please write staff.")]
    [InlineData("Use capital T.", "Use T.")]
    [InlineData("Press letter T.", "Press T.")]
    public void ApplyDefaultsFixesSpelledLetterDictation(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("Open the consenForm.", "Open the consent form.")]
    [InlineData("Open the consentForm.", "Open the consent form.")]
    [InlineData("ConsenForm is ready.", "Consent form is ready.")]
    [InlineData("The consent form is ready.", "The consent form is ready.")]
    public void ApplyDefaultsFixesConsentFormCompoundVariants(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("Dokumen everything and then make a checklist.", "Document everything and then make a checklist.")]
    [InlineData("Please dokumen this and continue.", "Please document this and continue.")]
    [InlineData("Can you dokumen that for me?", "Can you document that for me?")]
    [InlineData("We should dokumen it before moving on.", "We should document it before moving on.")]
    public void ApplyDefaultsFixesNarrowEnglishDokumenDrift(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("Weight why is it doing that?", "Wait, why is it doing that?")]
    [InlineData("Weight why is it doing that.", "Wait, why is it doing that?")]
    [InlineData("Weight what happened?", "Wait, what happened?")]
    [InlineData("Weight what happened.", "Wait, what happened?")]
    [InlineData("Weight how does this work?", "Wait, how does this work?")]
    [InlineData("Weight how does this work.", "Wait, how does this work?")]
    [InlineData("Weight a minute.", "Wait a minute.")]
    public void ApplyDefaultsFixesConversationalWeightAsWait(string input, string expected)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(expected, corrected);
    }

    [Theory]
    [InlineData("The body weight is 70 kg.")]
    [InlineData("Check the weight on the scale.")]
    [InlineData("The weight is heavy.")]
    public void ApplyDefaultsPreservesMeasurementWeight(string input)
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults(input);

        Assert.Equal(input, corrected);
    }

    [Fact]
    public void ApplyDefaultsPreservesUnrelatedText()
    {
        var corrected = VocabularyCorrectionService.ApplyDefaults("Testing one, two, three.");

        Assert.Equal("Testing one, two, three.", corrected);
    }
}
