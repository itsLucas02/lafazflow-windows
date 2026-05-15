using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class MiniRecorderVisualSpecTests
{
    [Fact]
    public void LayoutConstantsMatchBottomMiniReference()
    {
        Assert.Equal(184, MiniRecorderVisualSpec.CompactWidth);
        Assert.Equal(300, MiniRecorderVisualSpec.ExpandedWidth);
        Assert.Equal(40, MiniRecorderVisualSpec.ControlBarHeight);
        Assert.Equal(20, MiniRecorderVisualSpec.CompactCornerRadius);
        Assert.Equal(14, MiniRecorderVisualSpec.ExpandedCornerRadius);
        Assert.Equal(56, MiniRecorderVisualSpec.LiveTranscriptPanelHeight);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public void VisualizerBarsStayFlatWhenInactiveOrSilent(bool isRecording, double expectedHeight)
    {
        var height = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 0,
            timeSeconds: 0,
            isRecording: isRecording);

        Assert.Equal(expectedHeight, height, precision: 6);
    }

    [Fact]
    public void VisualizerBarHeightNeverExceedsReferenceMaximum()
    {
        var height = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 1,
            timeSeconds: 0.2,
            isRecording: true);

        Assert.InRange(height, MiniRecorderVisualSpec.BarMinHeight, MiniRecorderVisualSpec.BarMaxHeight);
    }

    [Fact]
    public void ProcessingPulseUsesReferenceTranscribingRhythm()
    {
        Assert.Equal(180, MiniRecorderVisualSpec.TranscribingPulseMilliseconds);
    }

    [Fact]
    public void WindowEntranceAndExitUseShortReferenceStyleMotion()
    {
        Assert.Equal(180, MiniRecorderVisualSpec.WindowEntranceMilliseconds);
        Assert.Equal(160, MiniRecorderVisualSpec.WindowExitMilliseconds);
        Assert.Equal(0.96, MiniRecorderVisualSpec.WindowEntranceStartScale, precision: 6);
    }
}
