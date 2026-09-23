using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class AppVersionTextTests
{
    [Fact]
    public void CompactUsesAssemblyMajorMinorPatchFormat()
    {
        var version = typeof(AppVersionText).Assembly.GetName().Version;

        Assert.NotNull(version);
        Assert.Equal($"v{version.Major}.{version.Minor}.{version.Build}", AppVersionText.Compact);
    }

    [Fact]
    public void SettingsTitleIncludesCompactVersion()
    {
        Assert.Equal($"LafazFlow Settings - {AppVersionText.Compact}", AppVersionText.SettingsTitle);
    }

    [Fact]
    public void TrayHeaderIncludesCompactVersion()
    {
        Assert.Equal($"LafazFlow {AppVersionText.Compact}", AppVersionText.TrayHeader);
    }

    [Fact]
    public void CommitHashIsShortHexOrDev()
    {
        Assert.Matches(@"^([0-9a-f]{7}|dev)$", AppVersionText.CommitHash);
    }

    [Fact]
    public void FullVersionIncludesReadableSequentialBuild()
    {
        var expected = AppVersionText.BuildNumber == "release"
            ? AppVersionText.Compact
            : $"{AppVersionText.Compact} build {AppVersionText.BuildNumber}";
        Assert.Equal(expected, AppVersionText.Full);
        Assert.StartsWith("v", AppVersionText.Full);
    }

    [Fact]
    public void BuildNumberIsSequentialOrDevelopmentFallback()
    {
        Assert.Matches(@"^(\d+|release)$", AppVersionText.BuildNumber);
    }
}
