using System.Net.Http;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

/// <summary>
/// Describes whether an update is available and where to get it.
/// </summary>
public sealed record UpdateInfo(bool IsUpdateAvailable, string CurrentVersion, string? LatestVersion, Uri? DownloadPage);

/// <summary>
/// Checks GitHub Releases for a newer LafazFlow version.
///
/// The latest-version fetch is injected as a delegate so the comparison logic is
/// unit-testable without any network or HTTP client. The production implementation
/// queries the GitHub "latest release" endpoint.
/// </summary>
public sealed partial class UpdateChecker
{
    private const string RepoOwner = "itsLucas02";
    private const string RepoName = "lafazflow-windows";

    private readonly Func<Task<string?>> _fetchLatestTag;
    private readonly string _currentVersion;
    private readonly Uri _releasesPage;

    public UpdateChecker(
        Func<Task<string?>>? fetchLatestTag = null,
        string? currentVersion = null)
    {
        _fetchLatestTag = fetchLatestTag ?? FetchLatestTagFromGitHubAsync;
        _currentVersion = currentVersion ?? CurrentAppVersion();
        _releasesPage = new Uri($"https://github.com/{RepoOwner}/{RepoName}/releases");
    }

    /// <summary>
    /// Returns update info. Never throws for transient network/parse failures;
    /// those are reported as "no update available" so a check failure never
    /// interrupts the user.
    /// </summary>
    public async Task<UpdateInfo> CheckAsync()
    {
        try
        {
            var latestTag = await _fetchLatestTag();
            if (string.IsNullOrWhiteSpace(latestTag))
            {
                return NoUpdate();
            }
            var latestVersion = ParseVersion(latestTag);
            if (latestVersion is null)
            {
                return NoUpdate();
            }

            var current = ParseVersion(_currentVersion);
            if (current is null)
            {
                return NoUpdate();
            }

            var newer = latestVersion > current;
            return new UpdateInfo(
                IsUpdateAvailable: newer,
                CurrentVersion: _currentVersion,
                LatestVersion: latestVersion.ToString(),
                DownloadPage: newer ? _releasesPage : null);
        }
        catch
        {
            return NoUpdate();
        }
    }

    private UpdateInfo NoUpdate() =>
        new(false, _currentVersion, null, null);

    /// <summary>Parses "v1.2.3" (or "1.2.3", or a 4-part "1.2.3.0") into a 3-part comparable version.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var match = VersionRegex().Match(tag.Trim());
        if (!match.Success)
        {
            return null;
        }

        var text = match.Groups["version"].Value.TrimStart('v');
        if (!System.Version.TryParse(text, out var parsed))
        {
            return null;
        }

        return new Version(parsed.Major, parsed.Minor, parsed.Build);
    }

    private static string CurrentAppVersion()
    {
        var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return assemblyVersion is null
            ? "0.0.0"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    private static async Task<string?> FetchLatestTagFromGitHubAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LafazFlow");
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return ParseTagName(json);
    }

    /// <summary>Extracts "tag_name" from the GitHub release JSON without a JSON dependency.</summary>
    public static string? ParseTagName(string json)
    {
        var match = TagNameRegex().Match(json);
        return match.Success ? match.Groups["tag"].Value : null;
    }

    [GeneratedRegex(@"""tag_name""\s*:\s*""(?<tag>v?\d+\.\d+\.\d+)""")]
    private static partial Regex TagNameRegex();

    [GeneratedRegex(@"(?<![\d.])(?<version>v?\d+\.\d+\.\d+(?:\.\d+)?)(?![\d.\-])")]
    private static partial Regex VersionRegex();
}
