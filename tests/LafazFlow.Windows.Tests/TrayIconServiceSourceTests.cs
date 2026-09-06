namespace LafazFlow.Windows.Tests;

public sealed class TrayIconServiceSourceTests
{
    [Fact]
    public void TrayMenuContainsDisabledVersionHeader()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Services", "TrayIconService.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        Assert.Contains("AppVersionText.TrayHeader", source);
        Assert.Contains("Enabled = false", source);
    }

    [Fact]
    public void TrayServiceShowsStartupNotificationViaBalloon()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Services", "TrayIconService.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        Assert.Contains("ShowStartupNotification", source);
        Assert.Contains("BalloonTipTitle", source);
        Assert.Contains("BalloonTipText", source);
        Assert.Contains("ShowBalloonTip(3000)", source);
    }

    [Fact]
    public void TrayMenuContainsCheckForUpdatesItem()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Services", "TrayIconService.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        Assert.Contains("Check for Updates…", source);
        Assert.Contains("_checkForUpdates", source);
        Assert.Contains("ShowUpdateNotification", source);
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
