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
