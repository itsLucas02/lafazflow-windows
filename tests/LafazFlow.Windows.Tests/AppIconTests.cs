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

    [Fact]
    public void IconContainsRequiredWindowsSizes()
    {
        var repoRoot = FindRepoRoot();
        var iconPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Resources", "Icons", "LafazFlow.ico");

        var sizes = ReadIconSizes(iconPath);

        // Windows 11 wants a 256px frame; the tray/title bar needs small frames.
        Assert.Contains(16, sizes);
        Assert.Contains(32, sizes);
        Assert.Contains(48, sizes);
        Assert.Contains(256, sizes);
    }

    [Fact]
    public void IconSourceMarkIsCommitted()
    {
        var repoRoot = FindRepoRoot();
        var markPath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "Resources", "Icons", "lafazflow-mark.png");

        Assert.True(File.Exists(markPath));
        Assert.True(new FileInfo(markPath).Length > 0);
    }

    private static List<int> ReadIconSizes(string iconPath)
    {
        var bytes = File.ReadAllBytes(iconPath);
        if (bytes.Length < 6 || bytes[2] != 1)
        {
            throw new InvalidOperationException("Not a valid ICO container.");
        }

        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = 6 + i * 16;
            var width = bytes[entry];
            var height = bytes[entry + 1];
            sizes.Add(width == 0 ? 256 : width);
            _ = height;
        }

        return sizes;
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
