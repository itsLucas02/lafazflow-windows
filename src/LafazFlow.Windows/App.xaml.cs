using LafazFlow.Windows.Services;

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

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _secondLaunchSignal?.Dispose();
        _secondLaunchSignal = null;
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }
}
