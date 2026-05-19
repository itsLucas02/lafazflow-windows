using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class AppCrashLogServiceTests
{
    [Fact]
    public void FormatCrashLineIncludesSafeCrashMetadata()
    {
        var line = AppCrashLogService.FormatCrashLine(
            "DispatcherUnhandledException",
            new InvalidOperationException(@"Failed at C:\Users\User\diagnostics\sample.wav transcript=spoken sample words; clipboard=clipboard sample"),
            new DateTimeOffset(2026, 5, 19, 14, 0, 0, TimeSpan.FromHours(8)));

        Assert.Contains("CRASH", line);
        Assert.Contains("source=DispatcherUnhandledException", line);
        Assert.Contains("type=InvalidOperationException", line);
        Assert.Contains("message=", line);
        Assert.DoesNotContain(@"C:\Users\User", line);
        Assert.DoesNotContain("spoken sample words", line);
        Assert.DoesNotContain("clipboard sample", line);
        Assert.Contains("<path>", line);
        Assert.Contains("transcript=<redacted>", line);
        Assert.Contains("clipboard=<redacted>", line);
    }

    [Fact]
    public void LogUnhandledExceptionAppendsCrashLineToConfiguredLog()
    {
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "lafazflow.log");
        var service = new AppCrashLogService(
            logPath,
            () => new DateTimeOffset(2026, 5, 19, 14, 1, 0, TimeSpan.FromHours(8)));

        service.LogUnhandledException("TaskScheduler.UnobservedTaskException", new ApplicationException("boom"));

        var log = File.ReadAllText(logPath);
        Assert.Contains("CRASH source=TaskScheduler.UnobservedTaskException type=ApplicationException", log);
        Assert.Contains("boom", log);
    }
}
