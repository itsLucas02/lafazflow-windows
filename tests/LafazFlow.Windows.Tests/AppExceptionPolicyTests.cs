using System.Windows.Media.Animation;

namespace LafazFlow.Windows.Tests;

public sealed class AppExceptionPolicyTests
{
    [Fact]
    public void AnimationExceptionsAreRecoverableDispatcherExceptions()
    {
        Assert.True(App.IsRecoverableDispatcherExceptionType(typeof(AnimationException)));
    }

    [Fact]
    public void NonAnimationExceptionsRemainFatalByDefault()
    {
        Assert.False(App.IsRecoverableDispatcherException(new InvalidOperationException("ordinary failure")));
    }
}
