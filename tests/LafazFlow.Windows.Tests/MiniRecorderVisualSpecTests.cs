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

    [Fact]
    public void VisualizerBarsStayFlatWhenInactive()
    {
        var height = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 0,
            timeSeconds: 0,
            isRecording: false);

        Assert.Equal(4, height, precision: 6);
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
    public void ProcessingDotsUseTrailingCascadeOpacity()
    {
        Assert.Equal(0.88, MiniRecorderVisualSpec.CalculateProcessingDotOpacity(2, 2), precision: 6);
        Assert.Equal(0.58, MiniRecorderVisualSpec.CalculateProcessingDotOpacity(1, 2), precision: 6);
        Assert.Equal(0.34, MiniRecorderVisualSpec.CalculateProcessingDotOpacity(0, 2), precision: 6);
        Assert.Equal(0.22, MiniRecorderVisualSpec.CalculateProcessingDotOpacity(4, 2), precision: 6);
    }

    [Fact]
    public void QuietRecordingUsesSubtleIdleBreathingMotion()
    {
        var first = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 0,
            timeSeconds: 0,
            isRecording: true);
        var second = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 0,
            timeSeconds: 0.5,
            isRecording: true);

        Assert.InRange(first, MiniRecorderVisualSpec.BarMinHeight, MiniRecorderVisualSpec.BarMinHeight + 2);
        Assert.InRange(second, MiniRecorderVisualSpec.BarMinHeight, MiniRecorderVisualSpec.BarMinHeight + 2);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ActiveAudioBarsRiseHigherThanIdleBreathing()
    {
        var idle = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 0,
            timeSeconds: 0.25,
            isRecording: true);
        var active = MiniRecorderVisualSpec.CalculateBarHeight(
            index: 7,
            barCount: 15,
            smoothedAudioLevel: 0.8,
            timeSeconds: 0.25,
            isRecording: true);

        Assert.True(active > idle + 8);
    }

    [Fact]
    public void WindowEntranceAndExitUseShortReferenceStyleMotion()
    {
        Assert.Equal(180, MiniRecorderVisualSpec.WindowEntranceMilliseconds);
        Assert.Equal(160, MiniRecorderVisualSpec.WindowExitMilliseconds);
        Assert.Equal(0.96, MiniRecorderVisualSpec.WindowEntranceStartScale, precision: 6);
    }
}
