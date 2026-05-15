namespace LafazFlow.Windows.Services;

public interface IClipboardPasteService
{
    Task PasteAsync(
        string text,
        bool restoreClipboard,
        int restoreDelayMs,
        IntPtr targetWindow,
        CancellationToken cancellationToken);
}
