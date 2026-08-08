namespace LafazFlow.Windows.Tests;

public sealed class DoubleShiftHotkeyServiceSourceTests
{
    [Fact]
    public void ServiceFlagsAutoRepeatFromKeyDownState()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Services", "DoubleShiftHotkeyService.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        Assert.Contains("_shiftKeyDown", source);
        Assert.Contains("var isRepeat = _shiftKeyDown;", source);
        Assert.Contains("_shiftKeyDown = false;", source);
        Assert.Contains("_detector.RegisterKeyUp();", source);
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
