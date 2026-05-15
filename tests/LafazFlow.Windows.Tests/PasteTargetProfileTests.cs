using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class PasteTargetProfileTests
{
    [Theory]
    [InlineData("Cursor")]
    [InlineData("Code")]
    public void FromProcessNameUsesTerminalSafePasteForCursorLikeTargets(string processName)
    {
        var profile = PasteTargetProfile.FromProcessName(processName, requestedClipboardRestore: true);

        Assert.Equal(PasteKeyGesture.ControlShiftV, profile.Gesture);
        Assert.False(profile.ShouldRestoreClipboard);
        Assert.Equal(2, profile.MaxPasteAttempts);
    }

    [Theory]
    [InlineData("notepad")]
    [InlineData("chrome")]
    [InlineData(null)]
    public void FromProcessNameUsesNormalPasteForGenericTargets(string? processName)
    {
        var profile = PasteTargetProfile.FromProcessName(processName, requestedClipboardRestore: true);

        Assert.Equal(PasteKeyGesture.ControlV, profile.Gesture);
        Assert.True(profile.ShouldRestoreClipboard);
        Assert.Equal(1, profile.MaxPasteAttempts);
    }

    [Fact]
    public void FromProcessNameHonorsDisabledClipboardRestoreForGenericTargets()
    {
        var profile = PasteTargetProfile.FromProcessName("notepad", requestedClipboardRestore: false);

        Assert.False(profile.ShouldRestoreClipboard);
    }
}
