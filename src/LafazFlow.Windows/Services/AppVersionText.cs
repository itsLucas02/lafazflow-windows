using System.Reflection;

namespace LafazFlow.Windows.Services;

public static class AppVersionText
{
    public static string Compact
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "v0.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string CommitHash
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (informational is null)
            {
                return "dev";
            }

            var separator = informational.IndexOf('+');
            if (separator < 0 || separator >= informational.Length - 1)
            {
                return "dev";
            }

            var hash = informational[(separator + 1)..];
            return hash.Length > 7 ? hash[..7] : hash;
        }
    }

    public static string Full => $"{Compact} ({CommitHash})";

    public static string SettingsTitle => $"LafazFlow Settings - {Compact}";

    public static string TrayHeader => $"LafazFlow {Compact}";
}
