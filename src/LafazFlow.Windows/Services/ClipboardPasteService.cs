using System.Runtime.InteropServices;
using WpfClipboard = System.Windows.Clipboard;

namespace LafazFlow.Windows.Services;

public sealed class ClipboardPasteService
{
    private const byte VirtualKeyControl = 0x11;
    private const byte VirtualKeyV = 0x56;
    private const uint KeyEventKeyUp = 0x0002;

    public async Task PasteAsync(
        string text,
        bool restoreClipboard,
        int restoreDelayMs,
        IntPtr targetWindow,
        CancellationToken cancellationToken)
    {
        var previousText = restoreClipboard && WpfClipboard.ContainsText()
            ? WpfClipboard.GetText()
            : null;

        WpfClipboard.SetText(text);
        await Task.Delay(50, cancellationToken);

        if (targetWindow != IntPtr.Zero)
        {
            SetForegroundWindow(targetWindow);
            await Task.Delay(50, cancellationToken);
        }

        keybd_event(VirtualKeyControl, 0, 0, UIntPtr.Zero);
        keybd_event(VirtualKeyV, 0, 0, UIntPtr.Zero);
        keybd_event(VirtualKeyV, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(VirtualKeyControl, 0, KeyEventKeyUp, UIntPtr.Zero);

        if (restoreClipboard && previousText is not null)
        {
            await Task.Delay(Math.Max(restoreDelayMs, 250), cancellationToken);
            WpfClipboard.SetText(previousText);
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
