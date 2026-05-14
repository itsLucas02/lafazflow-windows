namespace LafazFlow.Windows.Services;

public static class ClipboardRestorePolicy
{
    public static bool ShouldRestore(string? targetProcessName, bool requestedRestore)
    {
        if (!requestedRestore)
        {
            return false;
        }

        return !IsCursorLikeTarget(targetProcessName);
    }

    private static bool IsCursorLikeTarget(string? processName)
    {
        return string.Equals(processName, "Cursor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "Code", StringComparison.OrdinalIgnoreCase);
    }
}
