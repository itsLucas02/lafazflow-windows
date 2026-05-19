using LafazFlow.Windows.Services;
using System.Windows.Media.Animation;

namespace LafazFlow.Windows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\LafazFlow.Windows.SingleInstance";
    private const string SecondLaunchSignalName = @"Local\LafazFlow.Windows.ShowSettingsRequest";
    private Mutex? _singleInstanceMutex;
    private SecondLaunchSignal? _secondLaunchSignal;
    private readonly IAppCrashLogService _crashLogService = new AppCrashLogService();

    public static bool TryAcquireSingleInstance(string mutexName, out Mutex mutex)
    {
        mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        mutex.Dispose();
        return false;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        RegisterExceptionHandlers();

        if (!TryAcquireSingleInstance(SingleInstanceMutexName, out _singleInstanceMutex))
        {
            SecondLaunchSignal.Signal(SecondLaunchSignalName);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
        _secondLaunchSignal = SecondLaunchSignal.Listen(
            SecondLaunchSignalName,
            () => Dispatcher.BeginInvoke(() =>
            {
                if (MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ShowSettingsFromShell();
                }
        }));
    }

    public static bool IsRecoverableDispatcherException(Exception exception)
    {
        return IsRecoverableDispatcherExceptionType(exception.GetType())
            || (exception.InnerException is not null && IsRecoverableDispatcherExceptionType(exception.InnerException.GetType()));
    }

    public static bool IsRecoverableDispatcherExceptionType(Type exceptionType)
    {
        return exceptionType == typeof(AnimationException);
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _crashLogService.LogUnhandledException("DispatcherUnhandledException", e.Exception);
        e.Handled = IsRecoverableDispatcherException(e.Exception);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _crashLogService.LogUnhandledException("AppDomain.UnhandledException", exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _crashLogService.LogUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _secondLaunchSignal?.Dispose();
        _secondLaunchSignal = null;
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }
}
