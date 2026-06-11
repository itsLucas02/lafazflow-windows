namespace LafazFlow.Windows.Tests;

public sealed class SettingsWindowXamlTests
{
    [Fact]
    public void SettingsWindowUsesSidebarNavigationShell()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Width=\"860\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding SettingsSections}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedSection}\"", xaml);
        Assert.Contains("local:SettingsSection.Overview", xaml);
        Assert.Contains("local:SettingsSection.Dictation", xaml);
        Assert.Contains("local:SettingsSection.Models", xaml);
        Assert.Contains("local:SettingsSection.Vocabulary", xaml);
        Assert.Contains("local:SettingsSection.Sound", xaml);
        Assert.Contains("local:SettingsSection.Clipboard", xaml);
        Assert.Contains("local:SettingsSection.Diagnostics", xaml);
        Assert.Contains("local:SettingsSection.About", xaml);
    }

    [Fact]
    public void ReadOnlyFolderTextBoxesUseOneWayBinding()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Text=\"{Binding SettingsFolder, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding LogsFolder, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding RecordingsFolder, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void SettingsWindowContainsLatencyDiagnosticsViewer()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("ItemsSource=\"{Binding RecentLatencyRows}\"", xaml);
        Assert.Contains("Text=\"{Binding LatencyDiagnosticsMessage}\"", xaml);
        Assert.Contains("Text=\"{Binding LatestLatencySummary}\"", xaml);
        Assert.Contains("Binding=\"{Binding HotkeyToVisibleMs}\"", xaml);
        Assert.Contains("Binding=\"{Binding UiHideMs}\"", xaml);
        Assert.Contains("Click=\"RefreshLatency_OnClick\"", xaml);
        Assert.Contains("Click=\"ClearLatency_OnClick\"", xaml);
    }

    [Fact]
    public void SettingsWindowContainsSoundCueControls()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var codeBehindPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml.cs");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));
        var codeBehind = File.ReadAllText(Path.GetFullPath(codeBehindPath));

        Assert.Contains("Content=\"Play sound cues\"", xaml);
        Assert.Contains("IsChecked=\"{Binding EnableSoundCues}\"", xaml);
        Assert.Contains("Text=\"Master Volume (%)\"", xaml);
        Assert.Contains("Value=\"{Binding SoundCueVolumePercent, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("Text=\"Start\"", xaml);
        Assert.Contains("Text=\"Stop\"", xaml);
        Assert.Contains("Text=\"Done\"", xaml);
        Assert.Contains("Text=\"Error\"", xaml);
        Assert.Contains("Value=\"{Binding SoundCueRecordingStartedVolumePercent, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("Value=\"{Binding SoundCueTranscribingStartedVolumePercent, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("Value=\"{Binding SoundCueCompletedVolumePercent, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("Value=\"{Binding SoundCueErrorVolumePercent, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("Content=\"Test Start\"", xaml);
        Assert.Contains("Content=\"Test Stop\"", xaml);
        Assert.Contains("Content=\"Test Done\"", xaml);
        Assert.Contains("Content=\"Test Error\"", xaml);
        Assert.Contains("Click=\"TestStartSoundCue_OnClick\"", xaml);
        Assert.Contains("Click=\"TestStopSoundCue_OnClick\"", xaml);
        Assert.Contains("Click=\"TestDoneSoundCue_OnClick\"", xaml);
        Assert.Contains("Click=\"TestErrorSoundCue_OnClick\"", xaml);
        Assert.Contains("_soundCues.PlayRecordingStarted(BuildEditedSoundCueOptions())", codeBehind);
        Assert.Contains("_soundCues.PlayTranscribingStarted(BuildEditedSoundCueOptions())", codeBehind);
        Assert.Contains("_soundCues.PlayCompleted(BuildEditedSoundCueOptions())", codeBehind);
        Assert.Contains("_soundCues.PlayError(BuildEditedSoundCueOptions())", codeBehind);
        Assert.Contains("_viewModel.EnableSoundCues", codeBehind);
        Assert.Contains("_viewModel.SoundCueVolumePercent / 100.0", codeBehind);
        Assert.Contains("_viewModel.SoundCueRecordingStartedVolumePercent / 100.0", codeBehind);
        Assert.Contains("_viewModel.SoundCueTranscribingStartedVolumePercent / 100.0", codeBehind);
        Assert.Contains("_viewModel.SoundCueCompletedVolumePercent / 100.0", codeBehind);
        Assert.Contains("_viewModel.SoundCueErrorVolumePercent / 100.0", codeBehind);
    }

    [Fact]
    public void SettingsWindowContainsCustomVocabularyTextBox()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Text=\"Custom Vocabulary\"", xaml);
        Assert.Contains("Text=\"{Binding CustomVocabularyTerms, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("AcceptsReturn=\"True\"", xaml);
    }

    [Fact]
    public void SettingsWindowContainsCustomCorrectionRulesTextBox()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Text=\"Custom Correction Rules\"", xaml);
        Assert.Contains("heard phrase =&gt; corrected phrase", xaml);
        Assert.Contains("Text=\"{Binding CustomCorrectionRules, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("AcceptsReturn=\"True\"", xaml);
    }

    [Fact]
    public void SettingsWindowContainsRuntimeDiagnosticsControls()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Text=\"Runtime Status\"", xaml);
        Assert.Contains("Text=\"{Binding RuntimeProfileStatus}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding RuntimeDiagnosticRows}\"", xaml);
        Assert.Contains("Click=\"RefreshRuntimeDiagnostics_OnClick\"", xaml);
        Assert.Contains("Click=\"TestMicrophone_OnClick\"", xaml);
        Assert.Contains("Click=\"TestTranscription_OnClick\"", xaml);
        Assert.Contains("Click=\"ResetSettings_OnClick\"", xaml);
    }

    [Fact]
    public void SettingsWindowDoesNotModifyMiniRecorderShell()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.DoesNotContain("MiniRecorderWindow", xaml);
        Assert.DoesNotContain("RecorderShell", xaml);
        Assert.DoesNotContain("Visualizer", xaml);
    }

    [Fact]
    public void SettingsWindowShowsAppVersion()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Title=\"{Binding SettingsWindowTitle}\"", xaml);
        Assert.Contains("Text=\"{Binding AppVersion}\"", xaml);
    }

    [Fact]
    public void MiniRecorderWindowStaysOutOfTaskbar()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "MiniRecorderWindow.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("ShowInTaskbar=\"False\"", xaml);
        Assert.Contains("ShowActivated=\"False\"", xaml);
    }

    [Fact]
    public void MiniRecorderUsesOverlayPreviewWithoutResizingShell()
    {
        var repoRoot = FindRepoRoot();
        var xamlPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "MiniRecorderWindow.xaml");
        var codeBehindPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "MiniRecorderWindow.xaml.cs");
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));
        var codeBehind = File.ReadAllText(Path.GetFullPath(codeBehindPath));

        Assert.Contains("x:Name=\"LiveTranscriptOverlay\"", xaml);
        Assert.Contains("Height=\"40\"", xaml);
        Assert.DoesNotContain("TranscriptRow", xaml);
        Assert.DoesNotContain("Grid.RowDefinitions", xaml);
        Assert.Contains("UpdateLiveTranscriptOverlay", codeBehind);
        Assert.DoesNotContain("AnimateDouble(RecorderShell, WidthProperty", codeBehind);
        Assert.DoesNotContain("AnimateDouble(RecorderShell, HeightProperty", codeBehind);
    }

    [Fact]
    public void MiniRecorderShowSelfHealsInvisibleVisibleWindowState()
    {
        var repoRoot = FindRepoRoot();
        var codeBehindPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "UI", "MiniRecorderWindow.xaml.cs");
        var codeBehind = File.ReadAllText(Path.GetFullPath(codeBehindPath));

        Assert.Contains("var needsEntrance = !IsVisible || _isHiding || Opacity < 0.05;", codeBehind);
        Assert.Contains("if (needsEntrance)", codeBehind);
        Assert.Contains("_isHiding = false;", codeBehind);
        Assert.Contains("Opacity = 0;", codeBehind);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "LafazFlow.Windows")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
