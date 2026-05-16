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

    [Fact]
    public void LoadPopulatesRecentLatencyRows()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var logPath = CreateLatencyLog(
            "[2026-05-16T16:13:56.3366097+08:00] LATENCY id=abc123 status=completed model=ggml-base.en.bin threads=16 target=Cursor recording_ms=100 queue_wait_ms=0 whisper_ms=20 paste_ms=30 total_stop_to_done_ms=50 total_record_to_done_ms=150 error=none");
        var viewModel = SettingsViewModel.Load(
            new SettingsStore(root),
            new LatencyDiagnosticLogStore(logPath));

        Assert.Single(viewModel.RecentLatencyRows);
        Assert.Equal("abc123", viewModel.RecentLatencyRows[0].Id);
        Assert.Equal("Showing latest 1 latency entries.", viewModel.LatencyDiagnosticsMessage);
    }

    [Fact]
    public void RefreshLatencyDiagnosticsReloadsChangedLog()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var logPath = CreateLatencyLog("");
        var viewModel = SettingsViewModel.Load(
            new SettingsStore(root),
            new LatencyDiagnosticLogStore(logPath));
        File.WriteAllText(
            logPath,
            "[2026-05-16T16:13:56.3366097+08:00] LATENCY id=def456 status=failed model=ggml-base.en.bin threads=16 target=Antigravity recording_ms=100 queue_wait_ms=0 whisper_ms=20 paste_ms=na total_stop_to_done_ms=50 total_record_to_done_ms=150 error=InvalidOperationException");

        viewModel.RefreshLatencyDiagnostics();

        Assert.Single(viewModel.RecentLatencyRows);
        Assert.Equal("def456", viewModel.RecentLatencyRows[0].Id);
    }

    [Fact]
    public void ClearLatencyDiagnosticsRemovesLatencyRowsAndPreservesOtherLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var otherLog = "[2026-05-16T16:13:55.0000000+08:00] Ordinary log.";
        var logPath = CreateLatencyLog(
            $"""
            {otherLog}
            [2026-05-16T16:13:56.3366097+08:00] LATENCY id=abc123 status=completed model=ggml-base.en.bin threads=16 target=Cursor recording_ms=100 queue_wait_ms=0 whisper_ms=20 paste_ms=30 total_stop_to_done_ms=50 total_record_to_done_ms=150 error=none
            """);
        var viewModel = SettingsViewModel.Load(
            new SettingsStore(root),
            new LatencyDiagnosticLogStore(logPath));

        viewModel.ClearLatencyDiagnostics();

        Assert.Empty(viewModel.RecentLatencyRows);
        Assert.Equal("Cleared 1 latency entries.", viewModel.LatencyDiagnosticsMessage);
        Assert.Equal([otherLog], File.ReadAllLines(logPath));
    }

    private static string CreateLatencyLog(string content)
    {
        var logPath = Path.Combine(
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName,
            "lafazflow.log");
        File.WriteAllText(logPath, content);
        return logPath;
    }
}
