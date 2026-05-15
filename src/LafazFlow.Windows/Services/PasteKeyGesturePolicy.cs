namespace LafazFlow.Windows.Services;

public enum PasteKeyGesture
{
    ControlV,
    ControlShiftV
}

public static class PasteKeyGesturePolicy
{
    public static PasteKeyGesture GetGesture(string? targetProcessName)
    {
        return IsCursorLikeTarget(targetProcessName)
            ? PasteKeyGesture.ControlShiftV
            : PasteKeyGesture.ControlV;
    }

    private static bool IsCursorLikeTarget(string? processName)
    {
        return string.Equals(processName, "Cursor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "Code", StringComparison.OrdinalIgnoreCase);
    }
}
