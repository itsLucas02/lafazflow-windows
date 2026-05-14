using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class ClipboardRestorePolicyTests
{
    [Fact]
    public void ShouldRestoreReturnsFalseForCursorTargets()
    {
        Assert.False(ClipboardRestorePolicy.ShouldRestore("Cursor", requestedRestore: true));
        Assert.False(ClipboardRestorePolicy.ShouldRestore("Code", requestedRestore: true));
    }

    [Fact]
    public void ShouldRestoreKeepsRequestedBehaviorForGenericTargets()
    {
        Assert.True(ClipboardRestorePolicy.ShouldRestore("notepad", requestedRestore: true));
        Assert.False(ClipboardRestorePolicy.ShouldRestore("notepad", requestedRestore: false));
    }
}
