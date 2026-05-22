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
}
