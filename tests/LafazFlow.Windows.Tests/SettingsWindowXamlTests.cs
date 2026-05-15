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
