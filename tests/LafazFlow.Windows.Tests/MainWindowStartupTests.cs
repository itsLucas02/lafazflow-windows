namespace LafazFlow.Windows.Tests;

public sealed class MainWindowStartupTests
{
    [Fact]
    public void MainWindowStartsHiddenAndOutOfTaskbar()
    {
        var repoRoot = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "MainWindow.xaml"));

        Assert.Contains("Visibility=\"Hidden\"", xaml);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml);
        Assert.Contains("ShowActivated=\"False\"", xaml);
        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.DoesNotContain("Height=\"450\"", xaml);
        Assert.DoesNotContain("Width=\"800\"", xaml);
    }

    [Fact]
    public void AppStartupInitializesHiddenShellInsteadOfShowingMainWindow()
    {
        var repoRoot = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "App.xaml.cs"));
        var startupStart = code.IndexOf("protected override void OnStartup", StringComparison.Ordinal);
        var startupEnd = code.IndexOf("public static bool IsRecoverableDispatcherException", StringComparison.Ordinal);
        var startupBody = code[startupStart..startupEnd];

        Assert.Contains("_mainWindow.InitializeShell();", startupBody);
        Assert.DoesNotContain("MainWindow.Show();", startupBody);
    }

    [Fact]
    public void AppShowsSetupOnFirstRunAndMarksOnboardingComplete()
    {
        var repoRoot = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "App.xaml.cs"));
        var startupStart = code.IndexOf("protected override void OnStartup", StringComparison.Ordinal);
        var startupEnd = code.IndexOf("public static bool IsRecoverableDispatcherException", StringComparison.Ordinal);
        var startupBody = code[startupStart..startupEnd];

        Assert.Contains("_mainWindow.IsFirstRun", startupBody);
        Assert.Contains("_mainWindow.ShowSettingsFromShell();", startupBody);
        Assert.Contains("_mainWindow.MarkOnboardingComplete();", startupBody);
    }

    [Fact]
    public void ShellInitializationAcknowledgesSuccessfulStartup()
    {
        var repoRoot = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "MainWindow.xaml.cs"));
        var initStart = code.IndexOf("public void InitializeShell", StringComparison.Ordinal);
        var initEnd = code.IndexOf("private void OnLoaded", StringComparison.Ordinal);
        var initBody = code[initStart..initEnd];

        Assert.Contains("_trayIcon.ShowStartupNotification();", initBody);
        Assert.Contains("_hotkeyService.Start();", initBody);
    }

    [Fact]
    public void ShellInitializationStartsWorkerWithoutBlockingStartup()
    {
        var repoRoot = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "MainWindow.xaml.cs"));
        var initStart = code.IndexOf("public void InitializeShell", StringComparison.Ordinal);
        var initEnd = code.IndexOf("private void OnLoaded", StringComparison.Ordinal);
        var initBody = code[initStart..initEnd];

        Assert.Contains("Task.Run", initBody);
        Assert.Contains("GetReadySessionAsync", initBody);
        Assert.Contains("CancellationToken.None", initBody);
    }

    [Fact]
    public void RecorderReceivesWorkerEngineWhenAvailable()
    {
        var repoRoot = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "MainWindow.xaml.cs"));

        Assert.Contains("_workerEngine = new WorkerTranscriptionEngine", code);
        Assert.Contains("transcriptionEngine: _workerEngine", code);
        Assert.Contains("ResolveWorkerExecutable", code);
    }

    [Fact]
    public void DeliveryIsCommittedImmediatelyBeforePaste()
    {
        var repoRoot = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Services", "RecorderController.cs"));
        var pasteIndex = code.IndexOf("LatencyCheckpoint.PasteStarted", StringComparison.Ordinal);
        var pasteStart = code.IndexOf("PasteAsync", pasteIndex, StringComparison.Ordinal);
        var deliveryIndex = code.IndexOf("job.DeliveryCommitted = true;", StringComparison.Ordinal);

        Assert.True(deliveryIndex > 0);
        Assert.True(deliveryIndex < pasteStart);
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
