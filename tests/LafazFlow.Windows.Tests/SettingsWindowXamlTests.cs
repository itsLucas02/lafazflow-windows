namespace LafazFlow.Windows.Tests;

public sealed class SettingsWindowXamlTests
{
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
        var xaml = File.ReadAllText(Path.GetFullPath(xamlPath));

        Assert.Contains("Content=\"Play sound cues\"", xaml);
        Assert.Contains("IsChecked=\"{Binding EnableSoundCues}\"", xaml);
        Assert.Contains("Text=\"Sound Cue Volume (%)\"", xaml);
        Assert.Contains("Value=\"{Binding SoundCueVolumePercent, UpdateSourceTrigger=PropertyChanged}\"", xaml);
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
