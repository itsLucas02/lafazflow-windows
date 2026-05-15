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
        return PasteTargetProfile.FromProcessName(targetProcessName, requestedClipboardRestore: true).Gesture;
    }
}
