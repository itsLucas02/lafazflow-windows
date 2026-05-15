namespace LafazFlow.Windows.UI;

public static class MiniRecorderVisualSpec
{
    public const double CompactWidth = 184;
    public const double ExpandedWidth = 300;
    public const double ControlBarHeight = 40;
    public const double CompactCornerRadius = 20;
    public const double ExpandedCornerRadius = 14;
    public const double LiveTranscriptPanelHeight = 56;
    public const double BarWidth = 3;
    public const double BarSpacing = 2;
    public const double BarMinHeight = 4;
    public const double BarMaxHeight = 28;
    public const double AudioNoiseGate = 0.035;
    public const double AudioAttackWeight = 0.72;
    public const double AudioReleaseWeight = 0.22;
    public const double IdleBreathingAmplitude = 1.35;
    public const int BarCount = 15;
    public const int ProcessingPulseStepCount = 7;
    public const int TranscribingPulseMilliseconds = 180;
    public const int StateFadeMilliseconds = 200;
    public const int ExpansionMilliseconds = 300;
    public const int WindowEntranceMilliseconds = 180;
    public const int WindowExitMilliseconds = 160;
    public const double WindowEntranceStartScale = 0.96;

    public static double CalculateBarHeight(
        int index,
        int barCount,
        double smoothedAudioLevel,
        double timeSeconds,
        bool isRecording)
    {
        if (!isRecording)
        {
            return BarMinHeight;
        }

        var amplitude = Math.Pow(Math.Clamp(smoothedAudioLevel, 0, 1), 0.7);
        if (amplitude <= 0)
        {
            var breath = (Math.Sin(timeSeconds * 5.2 + index * 0.25) * 0.5 + 0.5) * IdleBreathingAmplitude;
            return Math.Clamp(BarMinHeight + breath, BarMinHeight, BarMinHeight + 2);
        }

        var wave = 0.45 + (Math.Sin(timeSeconds * 8 + index * 0.4) * 0.5 + 0.5) * 0.55;
        var centerDistance = Math.Abs(index - barCount / 2.0) / (barCount / 2.0);
        var centerBoost = 1.0 - centerDistance * 0.34;
        var height = BarMinHeight + amplitude * wave * centerBoost * (BarMaxHeight - BarMinHeight);

        return Math.Clamp(height, BarMinHeight, BarMaxHeight);
    }

    public static double SmoothAudioLevel(double currentLevel, double nextLevel, bool hasAudioSample)
    {
        var clamped = Math.Clamp(nextLevel, 0, 1);
        if (clamped < AudioNoiseGate)
        {
            clamped = 0;
        }

        if (!hasAudioSample)
        {
            return clamped;
        }

        var weight = clamped > currentLevel ? AudioAttackWeight : AudioReleaseWeight;
        var smoothed = currentLevel + (clamped - currentLevel) * weight;
        return smoothed < AudioNoiseGate ? 0 : smoothed;
    }

    public static double CalculateProcessingDotOpacity(int index, int activeStep)
    {
        if (index == activeStep)
        {
            return 0.88;
        }

        var trailingDistance = activeStep - index;
        return trailingDistance switch
        {
            1 => 0.58,
            2 => 0.34,
            _ => 0.22
        };
    }
}
