using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;
using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void SelectedSectionDefaultsToOverviewAndExposesAllSections()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var viewModel = SettingsViewModel.Load(store);

        Assert.Equal(SettingsSection.Overview, viewModel.SelectedSection);
        Assert.Equal(Enum.GetValues<SettingsSection>(), viewModel.SettingsSections);
    }

    [Fact]
    public void SelectedSectionRaisesPropertyChanged()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var viewModel = SettingsViewModel.Load(store);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.SelectedSection = SettingsSection.Sound;

        Assert.Equal(SettingsSection.Sound, viewModel.SelectedSection);
        Assert.Contains(nameof(SettingsViewModel.SelectedSection), changed);
    }

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
            KeepRecordingsForDiagnostics = true,
            TranscriptionProfile = TranscriptionProfile.Quality,
            WhisperBackend = WhisperBackend.Cuda,
            CudaWhisperCliPath = @"C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe",
            QualityModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin",
            EnableVad = true,
            VadModelPath = @"C:\Models\whisper\ggml-silero-v5.1.2.bin",
            EnableSoundCues = false,
            SoundCueVolume = 0.65,
            SoundCueRecordingStartedVolume = 0.8,
            SoundCueTranscribingStartedVolume = 0.9,
            SoundCueCompletedVolume = 1.7,
            SoundCueErrorVolume = 0.6,
            CustomVocabularyTerms = "PDPA\r\nCare Visit",
            CustomCorrectionRules = "superbiz => Supabase\r\nsteel document => stale document"
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
        Assert.Equal(TranscriptionProfile.Quality, viewModel.TranscriptionProfile);
        Assert.Equal(WhisperBackend.Cuda, viewModel.WhisperBackend);
        Assert.Equal(@"C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe", viewModel.CudaWhisperCliPath);
        Assert.Equal(@"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin", viewModel.QualityModelPath);
        Assert.True(viewModel.EnableVad);
        Assert.Equal(@"C:\Models\whisper\ggml-silero-v5.1.2.bin", viewModel.VadModelPath);
        Assert.False(viewModel.EnableSoundCues);
        Assert.Equal(65, viewModel.SoundCueVolumePercent);
        Assert.Equal(80, viewModel.SoundCueRecordingStartedVolumePercent);
        Assert.Equal(90, viewModel.SoundCueTranscribingStartedVolumePercent);
        Assert.Equal(170, viewModel.SoundCueCompletedVolumePercent);
        Assert.Equal(60, viewModel.SoundCueErrorVolumePercent);
        Assert.Equal("PDPA\r\nCare Visit", viewModel.CustomVocabularyTerms);
        Assert.Equal("superbiz => Supabase\r\nsteel document => stale document", viewModel.CustomCorrectionRules);
        Assert.Equal("Quality / CUDA / ggml-large-v3-turbo-q5_0.bin", viewModel.RuntimeProfileStatus);
        Assert.Equal(viewModel.AppVersion, viewModel.SettingsWindowTitle.Split(" - ")[1]);
        Assert.StartsWith("LafazFlow Settings - v", viewModel.SettingsWindowTitle);
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
        viewModel.TranscriptionProfile = TranscriptionProfile.Quality;
        viewModel.WhisperBackend = WhisperBackend.Cuda;
        viewModel.CudaWhisperCliPath = cliPath;
        viewModel.QualityModelPath = modelPath;
        viewModel.EnableVad = true;
        viewModel.VadModelPath = modelPath;
        viewModel.EnableSoundCues = false;
        viewModel.SoundCueVolumePercent = 72;
        viewModel.SoundCueRecordingStartedVolumePercent = 80;
        viewModel.SoundCueTranscribingStartedVolumePercent = 90;
        viewModel.SoundCueCompletedVolumePercent = 170;
        viewModel.SoundCueErrorVolumePercent = 60;
        viewModel.CustomVocabularyTerms = "PDPA\r\nCare Visit";
        viewModel.CustomCorrectionRules = "superbiz => Supabase\r\nstill document => stale document";

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
        Assert.Equal(TranscriptionProfile.Quality, saved.TranscriptionProfile);
        Assert.Equal(WhisperBackend.Cuda, saved.WhisperBackend);
        Assert.Equal(cliPath, saved.CudaWhisperCliPath);
        Assert.Equal(modelPath, saved.QualityModelPath);
        Assert.True(saved.EnableVad);
        Assert.Equal(modelPath, saved.VadModelPath);
        Assert.False(saved.EnableSoundCues);
        Assert.Equal(0.72, saved.SoundCueVolume, precision: 6);
        Assert.Equal(0.8, saved.SoundCueRecordingStartedVolume, precision: 6);
        Assert.Equal(0.9, saved.SoundCueTranscribingStartedVolume, precision: 6);
        Assert.Equal(1.7, saved.SoundCueCompletedVolume, precision: 6);
        Assert.Equal(0.6, saved.SoundCueErrorVolume, precision: 6);
        Assert.Equal("PDPA\r\nCare Visit", saved.CustomVocabularyTerms);
        Assert.Equal("superbiz => Supabase\r\nstill document => stale document", saved.CustomCorrectionRules);
    }

    [Fact]
    public void SaveRejectsMalformedCustomCorrectionRules()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = modelPath,
            CustomCorrectionRules = "superbiz => Supabase"
        });
        var viewModel = SettingsViewModel.Load(store);
        viewModel.CustomCorrectionRules = "broken rule";

        var result = viewModel.Save();

        var saved = store.Load();
        Assert.False(result.Success);
        Assert.Contains("Correction rule line 1 must use 'heard phrase => corrected phrase'.", result.Errors);
        Assert.Equal("superbiz => Supabase", saved.CustomCorrectionRules);

        File.Delete(cliPath);
        File.Delete(modelPath);
    }

    [Fact]
    public void SaveRejectsQualityProfileWhenQualityModelIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(store);
        viewModel.TranscriptionProfile = TranscriptionProfile.Quality;
        viewModel.QualityModelPath = @"C:\missing\ggml-large-v3-turbo-q5_0.bin";

        var result = viewModel.Save();

        Assert.False(result.Success);
        Assert.Contains("Quality model path does not exist.", result.Errors);
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
        Assert.Equal("Latest: total 50 ms, whisper 20 ms, paste 30 ms, queue 0 ms, hotkey na ms.", viewModel.LatestLatencySummary);
    }

    [Fact]
    public void LoadPopulatesRecentHotkeyRows()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var logPath = CreateLatencyLog("[2026-06-19T18:42:10.1234567+08:00] HOTKEY event=toggle_stop gesture=DoubleShift accepted=true state=Recording dispatch_ms=12 reason=second_shift target=Cursor");
        var viewModel = SettingsViewModel.Load(
            new SettingsStore(root),
            hotkeyDiagnostics: new HotkeyDiagnosticLogStore(logPath));

        Assert.Single(viewModel.RecentHotkeyRows);
        Assert.Equal("toggle_stop", viewModel.RecentHotkeyRows[0].Event);
        Assert.Equal("Showing latest 1 hotkey event.", viewModel.HotkeyDiagnosticsMessage);
        Assert.Equal("Latest: toggle_stop, state Recording, dispatch 12 ms, reason second_shift.", viewModel.LatestHotkeySummary);
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(140, 1)]
    public void SaveClampsSoundCueVolumePercent(double inputPercent, double expectedVolume)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(store);
        viewModel.SoundCueVolumePercent = inputPercent;

        var result = viewModel.Save();

        var saved = store.Load();
        Assert.True(result.Success);
        Assert.Equal(expectedVolume, saved.SoundCueVolume);
        Assert.Equal(expectedVolume * 100, viewModel.SoundCueVolumePercent);
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(250, 2)]
    public void SaveClampsPerCueSoundVolumePercents(double inputPercent, double expectedVolume)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(store);
        viewModel.SoundCueRecordingStartedVolumePercent = inputPercent;
        viewModel.SoundCueTranscribingStartedVolumePercent = inputPercent;
        viewModel.SoundCueCompletedVolumePercent = inputPercent;
        viewModel.SoundCueErrorVolumePercent = inputPercent;

        var result = viewModel.Save();

        var saved = store.Load();
        Assert.True(result.Success);
        Assert.Equal(expectedVolume, saved.SoundCueRecordingStartedVolume);
        Assert.Equal(expectedVolume, saved.SoundCueTranscribingStartedVolume);
        Assert.Equal(expectedVolume, saved.SoundCueCompletedVolume);
        Assert.Equal(expectedVolume, saved.SoundCueErrorVolume);
        Assert.Equal(expectedVolume * 100, viewModel.SoundCueRecordingStartedVolumePercent);
        Assert.Equal(expectedVolume * 100, viewModel.SoundCueTranscribingStartedVolumePercent);
        Assert.Equal(expectedVolume * 100, viewModel.SoundCueCompletedVolumePercent);
        Assert.Equal(expectedVolume * 100, viewModel.SoundCueErrorVolumePercent);
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
        Assert.Equal("Latest failed: total 50 ms, whisper 20 ms, paste na ms, queue 0 ms, hotkey na ms, error InvalidOperationException.", viewModel.LatestLatencySummary);
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
        Assert.Equal("No latency summary yet.", viewModel.LatestLatencySummary);
        Assert.Equal([otherLog], File.ReadAllLines(logPath));
    }

    [Fact]
    public void ClearHotkeyDiagnosticsRemovesHotkeyRowsAndPreservesOtherLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var otherLog = "[2026-06-19T18:42:09.0000000+08:00] Ordinary log.";
        var latencyLog = "[2026-06-19T18:42:10.0000000+08:00] LATENCY id=abc status=completed";
        var logPath = CreateLatencyLog(
            $"""
            {otherLog}
            {latencyLog}
            [2026-06-19T18:42:10.1234567+08:00] HOTKEY event=toggle_stop gesture=DoubleShift accepted=true state=Recording dispatch_ms=12 reason=second_shift target=Cursor
            """);
        var viewModel = SettingsViewModel.Load(
            new SettingsStore(root),
            hotkeyDiagnostics: new HotkeyDiagnosticLogStore(logPath));

        viewModel.ClearHotkeyDiagnostics();

        Assert.Empty(viewModel.RecentHotkeyRows);
        Assert.Equal("Cleared 1 hotkey event.", viewModel.HotkeyDiagnosticsMessage);
        Assert.Equal("No hotkey summary yet.", viewModel.LatestHotkeySummary);
        Assert.Equal([otherLog, latencyLog], File.ReadAllLines(logPath));
    }

    [Fact]
    public void LoadShowsNoLatencySummaryWhenLogIsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var logPath = CreateLatencyLog("");
        var viewModel = SettingsViewModel.Load(
            new SettingsStore(root),
            new LatencyDiagnosticLogStore(logPath),
            hotkeyDiagnostics: new HotkeyDiagnosticLogStore(logPath));

        Assert.Empty(viewModel.RecentLatencyRows);
        Assert.Equal("No latency entries yet.", viewModel.LatencyDiagnosticsMessage);
        Assert.Equal("No latency summary yet.", viewModel.LatestLatencySummary);
        Assert.Empty(viewModel.RecentHotkeyRows);
        Assert.Equal("No hotkey events yet.", viewModel.HotkeyDiagnosticsMessage);
        Assert.Equal("No hotkey summary yet.", viewModel.LatestHotkeySummary);
    }

    [Fact]
    public void RefreshRuntimeDiagnosticsPopulatesStatusRows()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var logsPath = Directory.CreateDirectory(Path.Combine(root, "logs")).FullName;
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(
            store,
            runtimeDiagnostics: new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe
            {
                ExistingFiles = [cliPath, modelPath],
                WritableDirectories = [logsPath],
                MicrophoneDeviceCount = 1
            }),
            logsFolderOverride: logsPath);

        viewModel.RefreshRuntimeDiagnostics();

        Assert.Equal("Fast / CPU / " + Path.GetFileName(modelPath), viewModel.RuntimeProfileStatus);
        Assert.Contains(viewModel.RuntimeDiagnosticRows, row => row.Name == "Whisper CLI" && row.Severity == RuntimeDiagnosticSeverity.Ok);
        Assert.Equal("Runtime ready.", viewModel.RuntimeDiagnosticsMessage);

        File.Delete(cliPath);
        File.Delete(modelPath);
    }

    [Fact]
    public async Task TestTranscriptionSmokeUpdatesRuntimeMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        var viewModel = SettingsViewModel.Load(
            store,
            runtimeDiagnostics: new RuntimeDiagnosticsService(new FakeRuntimeEnvironmentProbe
            {
                ExistingFiles = [cliPath, modelPath],
                SmokeResult = new RuntimeSmokeCheckResult(false, "Unable to start Whisper CLI.")
            }));

        await viewModel.TestTranscriptionSmokeAsync(CancellationToken.None);

        Assert.Contains("Unable to start Whisper CLI.", viewModel.RuntimeDiagnosticsMessage);

        File.Delete(cliPath);
        File.Delete(modelPath);
    }

    [Fact]
    public void ResetSettingsToDefaultsReloadsEditableProperties()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cliPath = Path.GetTempFileName();
        var modelPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = @"C:\custom\whisper-cli.exe",
            ModelPath = @"C:\custom\model.bin",
            WhisperThreads = 2
        });
        var viewModel = SettingsViewModel.Load(store);

        viewModel.ResetSettingsToDefaults();

        Assert.Equal(cliPath, viewModel.WhisperCliPath);
        Assert.Equal(modelPath, viewModel.ModelPath);
        Assert.Equal(AppSettings.Default.WhisperThreads, viewModel.WhisperThreads);
        Assert.Equal("", viewModel.CustomCorrectionRules);
        Assert.Equal("Settings reset to detected defaults.", viewModel.RuntimeDiagnosticsMessage);

        File.Delete(cliPath);
        File.Delete(modelPath);
    }

    [Fact]
    public void LoadExposesModelCardsWithInstalledAndActiveState()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modelRoot = Directory.CreateDirectory(Path.Combine(root, "models")).FullName;
        var modelPath = Path.Combine(modelRoot, "ggml-base.en.bin");
        File.WriteAllText(modelPath, "model");
        var cliPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath, defaultModelDirectory: modelRoot);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = modelPath,
            TranscriptionProfile = TranscriptionProfile.Fast
        });

        var viewModel = SettingsViewModel.Load(store, modelLibrary: new LocalModelLibraryService(modelRoot));

        var baseCard = viewModel.ModelCards.First(card => card.Id == "ggml-base.en");
        Assert.True(baseCard.IsInstalled);
        Assert.True(baseCard.IsActive);
        Assert.Equal("Active", baseCard.StatusLabel);
        Assert.Equal("142 MB model", baseCard.ModelFileLabel);
        Assert.Equal("Memory ~500 MB", baseCard.MemoryLabel);
        Assert.Equal("", baseCard.DownloadProgressLabel);
        Assert.Equal("●●●●●", baseCard.SpeedDots);
        Assert.False(string.IsNullOrWhiteSpace(baseCard.StatusBackground));
        Assert.False(string.IsNullOrWhiteSpace(baseCard.StatusBorder));
        Assert.False(string.IsNullOrWhiteSpace(baseCard.StatusForeground));
        Assert.Contains("Base English", viewModel.CurrentModelSummary);
        Assert.Contains(viewModel.ModelCards, card => card.Id == "ggml-large-v3-turbo-q5_0" && !card.IsInstalled);

        File.Delete(cliPath);
    }

    [Fact]
    public void DownloadProgressLabelOnlyShowsWhileDownloading()
    {
        var card = new ModelCardViewModel(
            LocalModelCatalog.Models.First(),
            @"C:\Models\whisper\ggml-base.en.bin",
            isInstalled: false,
            isActive: false);

        Assert.Equal("", card.DownloadProgressLabel);

        card.MarkDownloading(0.42);

        Assert.Equal("Downloading 42%", card.DownloadProgressLabel);
    }

    [Fact]
    public void UseModelMapsFastCardToFastProfileAndModelPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modelRoot = Directory.CreateDirectory(Path.Combine(root, "models")).FullName;
        var modelPath = Path.Combine(modelRoot, "ggml-small.en.bin");
        File.WriteAllText(modelPath, "model");
        var cliPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, modelPath, defaultModelDirectory: modelRoot);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = Path.Combine(modelRoot, "missing.bin"),
            QualityModelPath = modelPath,
            TranscriptionProfile = TranscriptionProfile.Quality
        });
        var viewModel = SettingsViewModel.Load(store, modelLibrary: new LocalModelLibraryService(modelRoot));
        var smallCard = viewModel.ModelCards.First(card => card.Id == "ggml-small.en");

        viewModel.UseModel(smallCard);

        Assert.Equal(TranscriptionProfile.Fast, viewModel.TranscriptionProfile);
        Assert.Equal(modelPath, viewModel.ModelPath);
        Assert.True(viewModel.ModelCards.First(card => card.Id == "ggml-small.en").IsActive);

        File.Delete(cliPath);
    }

    [Fact]
    public void UseModelMapsQualityCardToQualityProfileAndQualityModelPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modelRoot = Directory.CreateDirectory(Path.Combine(root, "models")).FullName;
        var qualityPath = Path.Combine(modelRoot, "ggml-large-v3-turbo-q5_0.bin");
        File.WriteAllText(qualityPath, "model");
        var cliPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, qualityPath, defaultModelDirectory: modelRoot);
        store.Save(AppSettings.Default with
        {
            WhisperCliPath = cliPath,
            ModelPath = qualityPath,
            QualityModelPath = Path.Combine(modelRoot, "missing-quality.bin"),
            WhisperBackend = WhisperBackend.Cuda,
            TranscriptionProfile = TranscriptionProfile.Fast
        });
        var viewModel = SettingsViewModel.Load(store, modelLibrary: new LocalModelLibraryService(modelRoot));
        var qualityCard = viewModel.ModelCards.First(card => card.Id == "ggml-large-v3-turbo-q5_0");

        viewModel.UseModel(qualityCard);

        Assert.Equal(TranscriptionProfile.Quality, viewModel.TranscriptionProfile);
        Assert.Equal(WhisperBackend.Cuda, viewModel.WhisperBackend);
        Assert.Equal(qualityPath, viewModel.QualityModelPath);

        File.Delete(cliPath);
    }

    [Fact]
    public async Task DownloadModelRefreshesCardState()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modelRoot = Directory.CreateDirectory(Path.Combine(root, "models")).FullName;
        var cliPath = Path.GetTempFileName();
        var store = new SettingsStore(root, cliPath, defaultModelDirectory: modelRoot);
        var viewModel = SettingsViewModel.Load(
            store,
            modelLibrary: new LocalModelLibraryService(modelRoot, new FakeModelDownloadClient("model")));
        var card = viewModel.ModelCards.First(model => model.Id == "ggml-base.en");

        await viewModel.DownloadModelAsync(card, CancellationToken.None);

        var refreshed = viewModel.ModelCards.First(model => model.Id == "ggml-base.en");
        Assert.True(refreshed.IsInstalled);
        Assert.Equal("Use Model", refreshed.PrimaryActionLabel);

        File.Delete(cliPath);
    }

    private static string CreateLatencyLog(string content)
    {
        var logPath = Path.Combine(
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName,
            "lafazflow.log");
        File.WriteAllText(logPath, content);
        return logPath;
    }

    private sealed class FakeRuntimeEnvironmentProbe : IRuntimeEnvironmentProbe
    {
        public HashSet<string> ExistingFiles { get; init; } = [];
        public HashSet<string> WritableDirectories { get; init; } = [];
        public int MicrophoneDeviceCount { get; init; } = 1;
        public RuntimeSmokeCheckResult SmokeResult { get; init; } = new(true, "Whisper CLI started.");

        public bool FileExists(string path) => ExistingFiles.Contains(path);

        public int GetMicrophoneDeviceCount() => MicrophoneDeviceCount;

        public bool CanWriteToDirectory(string path) => WritableDirectories.Contains(path);

        public Task<RuntimeSmokeCheckResult> RunWhisperSmokeCheckAsync(
            string whisperCliPath,
            string processPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SmokeResult);
        }
    }

    private sealed class FakeModelDownloadClient : IModelDownloadClient
    {
        private readonly string _content;

        public FakeModelDownloadClient(string content)
        {
            _content = content;
        }

        public Task DownloadAsync(
            Uri source,
            string destinationPath,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            progress.Report(1);
            File.WriteAllText(destinationPath, _content);
            return Task.CompletedTask;
        }
    }
}
