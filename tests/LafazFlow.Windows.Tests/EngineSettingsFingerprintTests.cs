using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class EngineSettingsFingerprintTests
{
    [Fact]
    public void SameSettingsProduceSameFingerprint()
    {
        Assert.Equal(
            EngineSettingsFingerprint.Compute(AppSettings.Default),
            EngineSettingsFingerprint.Compute(AppSettings.Default));
    }

    [Fact]
    public void EngineSettingChangesChangeTheFingerprint()
    {
        Assert.NotEqual(
            EngineSettingsFingerprint.Compute(AppSettings.Default),
            EngineSettingsFingerprint.Compute(AppSettings.Default with { WhisperThreads = 8 }));
        Assert.NotEqual(
            EngineSettingsFingerprint.Compute(AppSettings.Default),
            EngineSettingsFingerprint.Compute(AppSettings.Default with { ModelPath = @"C:\Models\other.bin" }));
        Assert.NotEqual(
            EngineSettingsFingerprint.Compute(AppSettings.Default),
            EngineSettingsFingerprint.Compute(AppSettings.Default with { EnableVad = true }));
        Assert.NotEqual(
            EngineSettingsFingerprint.Compute(AppSettings.Default),
            EngineSettingsFingerprint.Compute(AppSettings.Default with
            {
                TranscriptionProfile = TranscriptionProfile.Quality,
                WhisperBackend = WhisperBackend.Cuda
            }));
    }

    [Fact]
    public void PromptAndVocabularyDoNotChangeTheFingerprint()
    {
        Assert.Equal(
            EngineSettingsFingerprint.Compute(AppSettings.Default),
            EngineSettingsFingerprint.Compute(AppSettings.Default with
            {
                WhisperInitialPrompt = "a completely different custom prompt",
                CustomVocabularyTerms = "secret phrase",
                CustomCorrectionRules = "heard => fixed"
            }));
    }
}
