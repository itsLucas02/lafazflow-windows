namespace LafazFlow.Windows.Tests;

public sealed class AppIconTests
{
    [Fact]
    public void ProjectUsesBundledLafazFlowIcon()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "LafazFlow.Windows.csproj");
        var iconPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Resources", "Icons", "LafazFlow.ico");

        var project = File.ReadAllText(projectPath);

        Assert.Contains("<ApplicationIcon>Resources\\Icons\\LafazFlow.ico</ApplicationIcon>", project);
        Assert.True(File.Exists(iconPath));
        Assert.True(new FileInfo(iconPath).Length > 0);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "LafazFlow.Windows", "LafazFlow.Windows.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
