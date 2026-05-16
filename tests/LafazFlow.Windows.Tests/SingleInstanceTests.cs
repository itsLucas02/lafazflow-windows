using System.Threading;
using LafazFlow.Windows;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class SingleInstanceTests
{
    [Fact]
    public void SecondAcquireFailsWhileFirstMutexIsHeld()
    {
        var mutexName = $@"Local\LafazFlow.Windows.Tests.{Guid.NewGuid():N}";

        var firstAcquired = App.TryAcquireSingleInstance(mutexName, out var firstMutex);
        using var first = firstMutex;
        var secondAcquired = App.TryAcquireSingleInstance(mutexName, out var secondMutex);

        Assert.True(firstAcquired);
        Assert.False(secondAcquired);
        secondMutex.Dispose();
        first.ReleaseMutex();
    }

    [Fact]
    public async Task SecondLaunchSignalInvokesCallback()
    {
        var signalName = $@"Local\LafazFlow.Windows.Tests.ShowSettings.{Guid.NewGuid():N}";
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = SecondLaunchSignal.Listen(signalName, () => received.TrySetResult());

        SecondLaunchSignal.Signal(signalName);

        await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MainWindowStartupDoesNotForceMiniRecorderVisible()
    {
        var repoRoot = FindRepoRoot();
        var codePath = Path.Combine(repoRoot, "src", "LafazFlow.Windows", "MainWindow.xaml.cs");
        var code = File.ReadAllText(Path.GetFullPath(codePath));
        var onLoadedStart = code.IndexOf("private void OnLoaded", StringComparison.Ordinal);
        var onClosedStart = code.IndexOf("private void OnClosed", StringComparison.Ordinal);
        var onLoadedBody = code[onLoadedStart..onClosedStart];

        Assert.DoesNotContain("_miniRecorderWindow.ShowBottomCenter();", onLoadedBody);
        Assert.Contains("_hotkeyService.Start();", onLoadedBody);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "LafazFlow.Windows")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
