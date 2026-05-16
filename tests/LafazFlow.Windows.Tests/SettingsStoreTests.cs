using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenFileDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);

        var settings = store.Load();

        Assert.Equal("DoubleShift", settings.HotkeyGesture);
        Assert.Equal(HotkeyMode.Hybrid, settings.HotkeyMode);
        Assert.True(settings.RestoreClipboardAfterPaste);
        Assert.Equal(1500, settings.ClipboardRestoreDelayMs);
        Assert.Equal(16, settings.WhisperThreads);
        Assert.True(settings.AppendTrailingSpace);
        Assert.True(settings.ShowLiveTranscriptPreview);
        Assert.True(settings.EnableVocabularyCorrections);
        Assert.Contains("Supabase", settings.WhisperInitialPrompt);
        Assert.Contains("Luqman", settings.WhisperInitialPrompt);
        Assert.Contains("shadcn/ui", settings.WhisperInitialPrompt);
        Assert.Contains("components.json", settings.WhisperInitialPrompt);
        Assert.Contains("npx shadcn@latest", settings.WhisperInitialPrompt);
        Assert.Contains("build-web-apps:shadcn", settings.WhisperInitialPrompt);
        Assert.False(settings.KeepRecordingsForDiagnostics);
    }

    [Fact]
    public void SaveThenLoadRoundTripsSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var expected = AppSettings.Default with
        {
            HotkeyGesture = "Ctrl+Shift+D",
            HotkeyMode = HotkeyMode.Toggle,
            WhisperCliPath = @"C:\Tools\whisper-cli.exe",
            ModelPath = @"C:\Models\ggml-base.en.bin",
            AppendTrailingSpace = true,
            ClipboardRestoreDelayMs = 2000,
            WhisperThreads = 12,
            WhisperInitialPrompt = "Supabase Vercel Tailscale"
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LoadReturnsDetectedLocalWhisperPathsWhenFileDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var whisperCliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, whisperCliPath, modelPath);

        var settings = store.Load();

        Assert.Equal(whisperCliPath, settings.WhisperCliPath);
        Assert.Equal(modelPath, settings.ModelPath);

        File.Delete(whisperCliPath);
        File.Delete(modelPath);
    }

    [Fact]
    public void LoadPrefersBaseEnglishForDictationSpeedWhenNoSettingsFileExists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var whisperCliPath = Path.GetTempFileName();
        var modelRoot = Directory.CreateDirectory(Path.Combine(root, "models")).FullName;
        var baseModelPath = Path.Combine(modelRoot, "ggml-base.en.bin");
        var largeTurboPath = Path.Combine(modelRoot, "ggml-large-v3-turbo.bin");
        var quantizedLargeTurboPath = Path.Combine(modelRoot, "ggml-large-v3-turbo-q5_0.bin");
        File.WriteAllText(baseModelPath, "");
        File.WriteAllText(largeTurboPath, "");
        File.WriteAllText(quantizedLargeTurboPath, "");
        var store = new SettingsStore(root, whisperCliPath, baseModelPath, modelRoot);

        var settings = store.Load();

        Assert.Equal(baseModelPath, settings.ModelPath);
        File.Delete(whisperCliPath);
    }

    [Fact]
    public void LoadMigratesOldDefaultsButPreservesCustomModel()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modelRoot = Directory.CreateDirectory(Path.Combine(root, "models")).FullName;
        var oldDefaultPath = Path.Combine(modelRoot, "ggml-base.en.bin");
        var largeTurboPath = Path.Combine(modelRoot, "ggml-large-v3-turbo.bin");
        var quantizedLargeTurboPath = Path.Combine(modelRoot, "ggml-large-v3-turbo-q5_0.bin");
        File.WriteAllText(oldDefaultPath, "");
        File.WriteAllText(largeTurboPath, "");
        File.WriteAllText(quantizedLargeTurboPath, "");
        var store = new SettingsStore(root, defaultModelPath: oldDefaultPath, defaultModelDirectory: modelRoot);
        store.Save(AppSettings.Default with
        {
            SettingsSchemaVersion = 0,
            ModelPath = oldDefaultPath,
            ClipboardRestoreDelayMs = 250
        });

        var migrated = store.Load();

        Assert.Equal(oldDefaultPath, migrated.ModelPath);
        Assert.Equal(1500, migrated.ClipboardRestoreDelayMs);

        var customModelPath = Path.Combine(modelRoot, "custom.bin");
        File.WriteAllText(customModelPath, "");
        store.Save(AppSettings.Default with
        {
            SettingsSchemaVersion = 0,
            ModelPath = customModelPath,
            ClipboardRestoreDelayMs = 250
        });

        var custom = store.Load();

        Assert.Equal(customModelPath, custom.ModelPath);
    }

    [Fact]
    public void LoadMigratesPreviousDefaultPromptToDeveloperPrompt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        store.Save(AppSettings.Default with
        {
            SettingsSchemaVersion = 1,
            WhisperInitialPrompt = "Supabase, Vercel, Tailscale, Netlify, Mintlify, GitHub, PowerShell, Cursor, LafazFlow, Luqman."
        });

        var migrated = store.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, migrated.SettingsSchemaVersion);
        Assert.Contains("shadcn/ui", migrated.WhisperInitialPrompt);
        Assert.Contains("build-web-apps:shadcn", migrated.WhisperInitialPrompt);
    }

    [Fact]
    public void LoadPreservesCustomPromptDuringMigration()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        store.Save(AppSettings.Default with
        {
            SettingsSchemaVersion = 1,
            WhisperInitialPrompt = "My custom prompt"
        });

        var migrated = store.Load();

        Assert.Equal("My custom prompt", migrated.WhisperInitialPrompt);
    }
}
