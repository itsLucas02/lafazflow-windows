using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void LoadCopiesPersistedSettingsIntoEditableProperties()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = @"C:\Tools\whisper.cpp\Release\whisper-cli.exe",
            ModelPath = @"C:\Models\whisper\ggml-base.en.bin",
            WhisperThreads = 8,
            ShowLiveTranscriptPreview = false,
            EnableVocabularyCorrections = false,
            AppendTrailingSpace = false,
            RestoreClipboardAfterPaste = false,
            ClipboardRestoreDelayMs = 2500,
            KeepRecordingsForDiagnostics = true
        });

        var viewModel = SettingsViewModel.Load(store);

        Assert.Equal(@"C:\Tools\whisper.cpp\Release\whisper-cli.exe", viewModel.WhisperCliPath);
        Assert.Equal(@"C:\Models\whisper\ggml-base.en.bin", viewModel.ModelPath);
        Assert.Equal(8, viewModel.WhisperThreads);
        Assert.False(viewModel.ShowLiveTranscriptPreview);
        Assert.False(viewModel.EnableVocabularyCorrections);
        Assert.False(viewModel.AppendTrailingSpace);
        Assert.False(viewModel.RestoreClipboardAfterPaste);
        Assert.Equal(2500, viewModel.ClipboardRestoreDelayMs);
        Assert.True(viewModel.KeepRecordingsForDiagnostics);
    }

    [Fact]
    public void SavePersistsEditedSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(store);
        viewModel.WhisperThreads = 4;
        viewModel.ShowLiveTranscriptPreview = false;
        viewModel.EnableVocabularyCorrections = false;
        viewModel.AppendTrailingSpace = false;
        viewModel.RestoreClipboardAfterPaste = false;
        viewModel.ClipboardRestoreDelayMs = 2200;
        viewModel.KeepRecordingsForDiagnostics = true;

        var result = viewModel.Save();

        var saved = store.Load();
        Assert.True(result.Success);
        Assert.Equal(4, saved.WhisperThreads);
        Assert.False(saved.ShowLiveTranscriptPreview);
        Assert.False(saved.EnableVocabularyCorrections);
        Assert.False(saved.AppendTrailingSpace);
        Assert.False(saved.RestoreClipboardAfterPaste);
        Assert.Equal(2200, saved.ClipboardRestoreDelayMs);
        Assert.True(saved.KeepRecordingsForDiagnostics);
    }

    [Fact]
    public void SaveRejectsMissingPathsWithoutChangingPersistedSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = modelPath,
            WhisperThreads = 6
        });
        var viewModel = SettingsViewModel.Load(store);
        viewModel.WhisperCliPath = @"C:\missing\whisper-cli.exe";
        viewModel.ModelPath = @"C:\missing\model.bin";
        viewModel.WhisperThreads = 3;

        var result = viewModel.Save();

        var saved = store.Load();
        Assert.False(result.Success);
        Assert.Contains("Whisper CLI path does not exist.", result.Errors);
        Assert.Contains("Model path does not exist.", result.Errors);
        Assert.Equal(cliPath, saved.WhisperCliPath);
        Assert.Equal(modelPath, saved.ModelPath);
        Assert.Equal(6, saved.WhisperThreads);
    }

    [Fact]
    public void SaveClampsThreadAndDelayValues()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(store);
        viewModel.WhisperThreads = Environment.ProcessorCount + 50;
        viewModel.ClipboardRestoreDelayMs = 100;

        var result = viewModel.Save();

        var saved = store.Load();
        Assert.True(result.Success);
        Assert.Equal(Environment.ProcessorCount, saved.WhisperThreads);
        Assert.Equal(AppSettings.DefaultClipboardRestoreDelayMs, saved.ClipboardRestoreDelayMs);
    }
}
