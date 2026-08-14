namespace LafazFlow.Windows.Tests;

public sealed class ReleaseWorkflowTests
{
    private const string PinnedRevision = "968eebe77225d25e57a3f981da7c696310f0e881";

    [Fact]
    public void ReleaseWorkflowBuildsCpuWorkerFromPinnedRevisionAndNeverPublishesWithoutTag()
    {
        var workflow = ReadWorkflow();

        Assert.Contains($"ref: {PinnedRevision}", workflow);
        Assert.Contains("-Backend Cpu", workflow);
        Assert.Contains("compiled=cpu backend=cpu", workflow);
        Assert.Contains("-WorkerRevision $env:WHISPER_PINNED_REVISION", workflow);
        Assert.Contains("-WhisperCpuReleaseTag $env:WHISPER_CPU_RELEASE_TAG", workflow);
        Assert.Contains("LafazFlow-artifact-manifest.json", File.ReadAllText(
            Path.Combine(FindRepoRoot(), "scripts", "package-windows-release.ps1")));

        // Manual dispatch must never publish: the release step is tag-gated and
        // manual runs upload candidate artifacts only.
        Assert.Contains("if: github.ref_type != 'tag'", workflow);
        Assert.Contains("if: github.ref_type == 'tag'", workflow);
        Assert.Contains("actions/upload-artifact", workflow);
        Assert.Contains("softprops/action-gh-release", workflow);
    }

    [Fact]
    public void ReleaseWorkflowValidatesTagAgainstProjectVersion()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("Validate tag matches project version", workflow);
        Assert.Contains("if ($projectVersion -ne $expectedVersion)", workflow);
        Assert.Contains("Refusing to publish a mismatched release", workflow);
    }

    [Fact]
    public void ReleaseWorkflowPinsActionsToCommitShas()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("actions/checkout@11d5960a326750d5838078e36cf38b85af677262", workflow);
        Assert.Contains("actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9", workflow);
        Assert.Contains("actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02", workflow);
        Assert.Contains("softprops/action-gh-release@3bb12739c298aeb8a4eeaf626c5b8d85266b0e65", workflow);
    }

    private static string ReadWorkflow()
    {
        var path = Path.Combine(FindRepoRoot(), ".github", "workflows", "release.yml");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LafazFlow.Windows.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the LafazFlow repository root.");
    }
}
