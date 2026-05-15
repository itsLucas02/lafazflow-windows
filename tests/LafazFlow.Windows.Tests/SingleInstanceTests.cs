using System.Threading;
using LafazFlow.Windows;

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
}
