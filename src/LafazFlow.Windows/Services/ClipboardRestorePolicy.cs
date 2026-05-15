namespace LafazFlow.Windows.Services;

public static class ClipboardRestorePolicy
{
    public static bool ShouldRestore(string? targetProcessName, bool requestedRestore)
    {
        return PasteTargetProfile.FromProcessName(targetProcessName, requestedRestore).ShouldRestoreClipboard;
    }
}
