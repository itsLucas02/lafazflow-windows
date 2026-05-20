using LafazFlow.Windows.UI;

namespace LafazFlow.Windows.Tests;

public sealed class MiniRecorderVisualSpecTests
{
    [Fact]
    public void LayoutConstantsMatchBottomMiniReference()
    {
        Assert.Equal(184, MiniRecorderVisualSpec.CompactWidth);
        Assert.Equal(208, MiniRecorderVisualSpec.BalancedCompactWidth);
        Assert.Equal(300, MiniRecorderVisualSpec.ExpandedWidth);
        Assert.True(MiniRecorderVisualSpec.BalancedCompactWidth > MiniRecorderVisualSpec.CompactWidth);
        Assert.True(MiniRecorderVisualSpec.CompactMaxWidth >= MiniRecorderVisualSpec.ExpandedWidth);
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
        Assert.Equal(145, MiniRecorderVisualSpec.TranscribingPulseMilliseconds);
        Assert.Equal(7, MiniRecorderVisualSpec.ProcessingDotCount);
        Assert.Equal(MiniRecorderVisualSpec.ProcessingDotCount, MiniRecorderVisualSpec.ProcessingPulseStepCount);
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
    public void ProcessingPulseHasActiveDotForEveryStep()
    {
        for (var step = 0; step < MiniRecorderVisualSpec.ProcessingPulseStepCount; step++)
        {
            var activeDotCount = Enumerable
                .Range(0, MiniRecorderVisualSpec.ProcessingDotCount)
                .Count(index => Math.Abs(MiniRecorderVisualSpec.CalculateProcessingDotOpacity(index, step) - 0.88) < 0.001);

            Assert.Equal(1, activeDotCount);
        }
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
    public void AudioBarColorUsesAquaPaletteByHeight()
    {
        var shortColor = MiniRecorderVisualSpec.CalculateAudioBarColor(MiniRecorderVisualSpec.BarMinHeight);
        var tallColor = MiniRecorderVisualSpec.CalculateAudioBarColor(MiniRecorderVisualSpec.BarMaxHeight);

        Assert.Equal(7, shortColor.Red);
        Assert.Equal(190, shortColor.Green);
        Assert.Equal(184, shortColor.Blue);
        Assert.Equal(196, tallColor.Red);
        Assert.Equal(255, tallColor.Green);
        Assert.Equal(249, tallColor.Blue);
    }

    [Fact]
    public void AudioBarColorBrightensAsBarsGrow()
    {
        var shortColor = MiniRecorderVisualSpec.CalculateAudioBarColor(MiniRecorderVisualSpec.BarMinHeight + 1);
        var tallColor = MiniRecorderVisualSpec.CalculateAudioBarColor(MiniRecorderVisualSpec.BarMaxHeight - 1);

        Assert.True(tallColor.Red > shortColor.Red);
        Assert.True(tallColor.Green > shortColor.Green);
        Assert.True(tallColor.Blue > shortColor.Blue);
    }

    [Fact]
    public void WindowEntranceAndExitUseShortReferenceStyleMotion()
    {
        Assert.Equal(140, MiniRecorderVisualSpec.WindowEntranceMilliseconds);
        Assert.Equal(120, MiniRecorderVisualSpec.WindowExitMilliseconds);
        Assert.Equal(120, MiniRecorderVisualSpec.StateFadeMilliseconds);
        Assert.Equal(220, MiniRecorderVisualSpec.ExpansionMilliseconds);
        Assert.Equal(16, MiniRecorderVisualSpec.RenderFrameThrottleMilliseconds);
        Assert.Equal(0.985, MiniRecorderVisualSpec.WindowEntranceStartScale, precision: 6);
        Assert.Equal(5, MiniRecorderVisualSpec.WindowEntranceTranslateY);
        Assert.Equal(3, MiniRecorderVisualSpec.WindowExitTranslateY);
    }

    [Fact]
    public void AudioSmoothingSoftensSuddenDropsButStillRespondsToSpeech()
    {
        var rise = MiniRecorderVisualSpec.SmoothAudioLevel(0.1, 0.9, hasAudioSample: true);
        var drop = MiniRecorderVisualSpec.SmoothAudioLevel(0.9, 0.1, hasAudioSample: true);

        Assert.True(rise > 0.6);
        Assert.True(drop > 0.7);
        Assert.True(drop < 0.9);
    }

    [Fact]
    public void AnimationOriginFallsBackWhenCurrentValueIsNotConcrete()
    {
        Assert.Equal(208, MiniRecorderVisualSpec.ResolveAnimationOrigin(double.NaN, 208));
        Assert.Equal(208, MiniRecorderVisualSpec.ResolveAnimationOrigin(double.PositiveInfinity, 208));
        Assert.Equal(184, MiniRecorderVisualSpec.ResolveAnimationOrigin(184, 208));
    }
}
