using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v2.0.0", "2.0.0")]
    [InlineData("v1.2.3.0", "1.2.3")]
    [InlineData("release v1.2.3", "1.2.3")]
    [InlineData("  v10.20.30  ", "10.20.30")]
    public void ParseVersionExtractsComparableVersion(string tag, string expected)
    {
        var parsed = UpdateChecker.ParseVersion(tag);
        Assert.NotNull(parsed);
        Assert.Equal(expected, parsed!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1.2")]
    [InlineData("not-a-version")]
    [InlineData("v1.2.3-rc1")]
    public void ParseVersionRejectsInvalidTags(string tag)
    {
        Assert.Null(UpdateChecker.ParseVersion(tag));
    }

    [Fact]
    public async Task CheckReportsUpdateWhenLatestIsNewer()
    {
        var checker = new UpdateChecker(
            fetchLatestTag: () => Task.FromResult<string?>("v2.0.0"),
            currentVersion: "1.1.1");

        var info = await checker.CheckAsync();

        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("1.1.1", info.CurrentVersion);
        Assert.Equal("2.0.0", info.LatestVersion);
        Assert.NotNull(info.DownloadPage);
        Assert.Contains("releases", info.DownloadPage!.ToString());
    }

    [Fact]
    public async Task CheckReportsNoUpdateWhenCurrentIsNewerOrEqual()
    {
        var checker = new UpdateChecker(
            fetchLatestTag: () => Task.FromResult<string?>("v1.1.0"),
            currentVersion: "1.1.1");

        var info = await checker.CheckAsync();

        Assert.False(info.IsUpdateAvailable);
        Assert.Equal("1.1.0", info.LatestVersion);
        Assert.Null(info.DownloadPage);
    }

    [Fact]
    public async Task CheckReportsNoUpdateOnFetchFailureWithoutThrowing()
    {
        var checker = new UpdateChecker(
            fetchLatestTag: () => Task.FromException<string?>(new HttpRequestException("offline")),
            currentVersion: "1.1.1");

        var info = await checker.CheckAsync();

        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckReportsNoUpdateWhenFetchReturnsUnparsableTag()
    {
        var checker = new UpdateChecker(
            fetchLatestTag: () => Task.FromResult<string?>("not-a-version"),
            currentVersion: "1.1.1");

        var info = await checker.CheckAsync();

        Assert.False(info.IsUpdateAvailable);
    }

    [Theory]
    [InlineData("""{"tag_name":"v1.1.0"}""", "v1.1.0")]
    [InlineData("""{"name":"x","tag_name":"v2.3.4","draft":false}""", "v2.3.4")]
    [InlineData("""{"tag_name":"1.0.0"}""", "1.0.0")]
    [InlineData("not json at all", null)]
    [InlineData("""{"no_tag":true}""", null)]
    public void ParseTagNameExtractsGitHubTag(string json, string? expected)
    {
        Assert.Equal(expected, UpdateChecker.ParseTagName(json));
    }
}
