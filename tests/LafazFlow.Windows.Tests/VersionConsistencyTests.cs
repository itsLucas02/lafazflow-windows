namespace LafazFlow.Windows.Tests;

public sealed class VersionConsistencyTests
{
    [Fact]
    public void ProjectInstallerPackageAndExpectedTagAgreeOnVersion()
    {
        var repoRoot = FindRepoRoot();
        var csproj = File.ReadAllText(Path.Combine(repoRoot, "src", "LafazFlow.Windows", "LafazFlow.Windows.csproj"));
        var installer = File.ReadAllText(Path.Combine(repoRoot, "scripts", "lafazflow-setup.iss"));
        var packager = File.ReadAllText(Path.Combine(repoRoot, "scripts", "package-windows-release.ps1"));

        var projectVersion = ExtractValue(csproj, "<Version>", "</Version>");
        var informationalVersion = ExtractValue(csproj, "<InformationalVersion>", "</InformationalVersion>");
        var installerVersion = ExtractValue(installer, "#define MyAppVersion \"", "\"");

        Assert.Equal("1.1.0", projectVersion);
        Assert.Equal("1.1.0", informationalVersion);
        Assert.Equal(projectVersion, installerVersion);
        Assert.Contains("LafazFlow-$Version-$Runtime-portable.zip", packager);
        Assert.Contains("LafazFlow-$Version-setup.exe", packager);
        Assert.Equal("LafazFlow-1.1.0-win-x64-portable.zip", $"LafazFlow-{projectVersion}-win-x64-portable.zip");
        Assert.Equal("LafazFlow-1.1.0-setup.exe", $"LafazFlow-{projectVersion}-setup.exe");
        Assert.Equal("v1.1.0", $"v{projectVersion}");
    }

    private static string ExtractValue(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker '{startMarker}' was not found.");
        start += startMarker.Length;
        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"End marker '{endMarker}' was not found.");
        return text[start..end];
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
