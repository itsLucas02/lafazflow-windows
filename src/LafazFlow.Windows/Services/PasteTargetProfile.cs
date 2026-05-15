namespace LafazFlow.Windows.Services;

public sealed record PasteTargetProfile(
    string? ProcessName,
    PasteKeyGesture Gesture,
    bool ShouldRestoreClipboard,
    int MaxPasteAttempts)
{
    public static PasteTargetProfile FromProcessName(string? processName, bool requestedClipboardRestore)
    {
        var isCursorLike = IsCursorLikeTarget(processName);
        return new PasteTargetProfile(
            processName,
            isCursorLike ? PasteKeyGesture.ControlShiftV : PasteKeyGesture.ControlV,
            requestedClipboardRestore && !isCursorLike,
            isCursorLike ? 2 : 1);
    }

    private static bool IsCursorLikeTarget(string? processName)
    {
        return string.Equals(processName, "Cursor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "Code", StringComparison.OrdinalIgnoreCase);
    }
}
