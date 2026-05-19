using System.IO;
using System.Text.RegularExpressions;

namespace LafazFlow.Windows.Services;

public sealed partial class AppCrashLogService : IAppCrashLogService
{
    private readonly string _logPath;
    private readonly Func<DateTimeOffset> _now;

    public AppCrashLogService()
        : this(DefaultLogPath(), () => DateTimeOffset.Now)
    {
    }

    public AppCrashLogService(string logPath, Func<DateTimeOffset>? now = null)
    {
        _logPath = logPath;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public void LogUnhandledException(string source, Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_logPath, FormatCrashLine(source, exception, _now()) + Environment.NewLine);
        }
        catch
        {
        }
    }

    public static string FormatCrashLine(string source, Exception exception, DateTimeOffset timestamp)
    {
        var safeSource = SanitizeToken(source);
        var safeType = SanitizeToken(exception.GetType().Name);
        var safeMessage = SanitizeMessage(exception.Message);
        return $"[{timestamp:O}] CRASH source={safeSource} type={safeType} message=\"{safeMessage}\"";
    }

    private static string DefaultLogPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LafazFlow",
            "Logs",
            "lafazflow.log");
    }

    private static string SanitizeToken(string value)
    {
        return TokenUnsafeCharactersRegex().Replace(value, "_");
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "No message";
        }

        var sanitized = message.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
        sanitized = WindowsPathRegex().Replace(sanitized, "<path>");
        sanitized = DataPayloadRegex().Replace(sanitized, "$1=<redacted>");
        sanitized = WhitespaceRegex().Replace(sanitized, " ").Trim();
        sanitized = sanitized.Replace("\"", "'");

        return sanitized.Length <= 220 ? sanitized : sanitized[..220] + "...";
    }

    [GeneratedRegex("[^A-Za-z0-9_.-]+")]
    private static partial Regex TokenUnsafeCharactersRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\[^\s""']+|\\\\[^\s""']+)")]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"(?i)\b(transcript|clipboard|audio|wav|text|content)\s*=\s*[^;,.]+")]
    private static partial Regex DataPayloadRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
