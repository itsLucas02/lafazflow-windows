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
    public const int BarCount = 15;
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
        var wave = Math.Sin(timeSeconds * 8 + index * 0.4) * 0.5 + 0.5;
        var centerDistance = Math.Abs(index - barCount / 2.0) / (barCount / 2.0);
        var centerBoost = 1.0 - centerDistance * 0.4;
        var height = BarMinHeight + amplitude * wave * centerBoost * (BarMaxHeight - BarMinHeight);

        return Math.Clamp(height, BarMinHeight, BarMaxHeight);
    }
}
