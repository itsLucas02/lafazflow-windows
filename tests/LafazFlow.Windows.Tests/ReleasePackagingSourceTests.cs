namespace LafazFlow.Windows.Tests;

public sealed class ReleasePackagingSourceTests
{
    [Fact]
    public void PackageScriptBuildsPortableReleaseWithSafetyChecks()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "package-windows-release.ps1");
        var script = File.ReadAllText(Path.GetFullPath(scriptPath));

        Assert.Contains("--self-contained", script);
        Assert.Contains("win-x64", script);
        Assert.Contains("whisper-bin-x64.zip", script);
        Assert.Contains("Compress-Archive", script);
        Assert.Contains("settings.json", script);
        Assert.Contains(".log", script);
        Assert.Contains(".wav", script);
        Assert.Contains(".bin", script);
        Assert.Contains("sk-[A-Za-z0-9]{20,}", script);
        Assert.Contains("ghp_", script);
        Assert.Contains("Inno Setup compiler was not provided", script);
    }

    [Fact]
    public void InstallerTemplateTargetsAppAndDesktopShortcut()
    {
        var repoRoot = FindRepoRoot();
        var issPath = Path.Combine(repoRoot, "scripts", "lafazflow-setup.iss");
        var iss = File.ReadAllText(Path.GetFullPath(issPath));

        Assert.Contains("[Setup]", iss);
        Assert.Contains("[Files]", iss);
        Assert.Contains("LafazFlow.Windows.exe", iss);
        Assert.Contains("[Icons]", iss);
        Assert.Contains("desktopicon", iss);
    }

    [Fact]
    public void ReleaseWorkflowBuildsPackagesAndPublishesArtifacts()
    {
        var repoRoot = FindRepoRoot();
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "release.yml");
        var workflow = File.ReadAllText(Path.GetFullPath(workflowPath));

        Assert.Contains("windows-latest", workflow);
        Assert.Contains("dotnet test LafazFlow.Windows.sln", workflow);
        Assert.Contains("innosetup", workflow);
        Assert.Contains("package-windows-release.ps1", workflow);
        Assert.Contains("action-gh-release", workflow);
        Assert.Contains("tags:", workflow);
        Assert.Contains("v*", workflow);
        Assert.Contains("artifacts/release/*.zip", workflow);
        Assert.Contains("artifacts/release/*.exe", workflow);
    }

    [Fact]
    public void RuntimeSetupDocsAndReadmeCoverEndUserFlow()
    {
        var repoRoot = FindRepoRoot();
        var docs = File.ReadAllText(Path.Combine(repoRoot, "docs", "windows-runtime-setup.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));

        Assert.Contains("SmartScreen", docs);
        Assert.Contains("double-press shift", docs.ToLowerInvariant());
        Assert.Contains("Settings > Models", docs);
        Assert.Contains("Releases", readme);
        Assert.Contains("portable", readme.ToLowerInvariant());
        Assert.Contains("setup.exe", readme);
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
