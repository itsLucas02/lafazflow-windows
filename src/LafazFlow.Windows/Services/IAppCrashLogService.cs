namespace LafazFlow.Windows.Services;

public interface IAppCrashLogService
{
    void LogUnhandledException(string source, Exception exception);
}
