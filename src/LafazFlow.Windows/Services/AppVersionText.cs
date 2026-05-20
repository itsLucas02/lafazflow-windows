using System.Reflection;

namespace LafazFlow.Windows.Services;

public static class AppVersionText
{
    public static string Compact
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "v0.0" : $"v{version.Major}.{version.Minor}";
        }
    }

    public static string SettingsTitle => $"LafazFlow Settings - {Compact}";

    public static string TrayHeader => $"LafazFlow {Compact}";
}
