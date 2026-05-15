using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class PasteKeyGesturePolicyTests
{
    [Theory]
    [InlineData("Cursor")]
    [InlineData("Code")]
    public void GetGestureUsesCtrlShiftVForCursorLikeTargets(string processName)
    {
        var gesture = PasteKeyGesturePolicy.GetGesture(processName);

        Assert.Equal(PasteKeyGesture.ControlShiftV, gesture);
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("WindowsTerminal")]
    [InlineData(null)]
    public void GetGestureUsesCtrlVForGenericTargets(string? processName)
    {
        var gesture = PasteKeyGesturePolicy.GetGesture(processName);

        Assert.Equal(PasteKeyGesture.ControlV, gesture);
    }
}
