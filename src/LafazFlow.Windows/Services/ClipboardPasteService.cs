using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;

namespace LafazFlow.Windows.Services;

public sealed class ClipboardPasteService : IClipboardPasteService
{
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public async Task PasteAsync(
        string text,
        bool restoreClipboard,
        int restoreDelayMs,
        IntPtr targetWindow,
        CancellationToken cancellationToken)
    {
        var targetProcessName = GetProcessName(targetWindow);
        var shouldRestoreClipboard = ClipboardRestorePolicy.ShouldRestore(
            targetProcessName,
            restoreClipboard);
        var pasteGesture = PasteKeyGesturePolicy.GetGesture(targetProcessName);
        var previousClipboard = shouldRestoreClipboard
            ? await GetClipboardSnapshotWithRetryAsync(cancellationToken)
            : null;

        await SetTextWithRetryAsync(text, cancellationToken);
        await Task.Delay(50, cancellationToken);

        if (targetWindow != IntPtr.Zero)
        {
            SetForegroundWindow(targetWindow);
            await Task.Delay(50, cancellationToken);
        }

        SendPasteGesture(pasteGesture);

        if (shouldRestoreClipboard && previousClipboard is not null)
        {
            await Task.Delay(Math.Max(restoreDelayMs, 1500), cancellationToken);
            await SetClipboardDataWithRetryAsync(previousClipboard, cancellationToken);
        }
    }

    private static async Task<System.Windows.IDataObject?> GetClipboardSnapshotWithRetryAsync(CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var source = WpfClipboard.GetDataObject();
                if (source is null)
                {
                    return null;
                }

                var snapshot = new System.Windows.DataObject();
                foreach (var format in source.GetFormats(autoConvert: false))
                {
                    var data = source.GetData(format, autoConvert: false);
                    if (data is not null)
                    {
                        snapshot.SetData(format, data);
                    }
                }

                return snapshot;
            }
            catch (Exception error)
            {
                lastError = error;
                await Task.Delay(40, cancellationToken);
            }
        }

        throw new InvalidOperationException("Clipboard data could not be read.", lastError);
    }

    private static async Task SetClipboardDataWithRetryAsync(System.Windows.IDataObject dataObject, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                WpfClipboard.SetDataObject(dataObject, true);
                return;
            }
            catch (Exception error)
            {
                lastError = error;
                await Task.Delay(40, cancellationToken);
            }
        }

        throw new InvalidOperationException("Clipboard data could not be restored.", lastError);
    }

    private static async Task SetTextWithRetryAsync(string text, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                WpfClipboard.SetText(text);
                return;
            }
            catch (Exception error)
            {
                lastError = error;
                await Task.Delay(40, cancellationToken);
            }
        }

        throw new InvalidOperationException("Clipboard text could not be written.", lastError);
    }

    private static void SendPasteGesture(PasteKeyGesture gesture)
    {
        if (gesture == PasteKeyGesture.ControlShiftV)
        {
            SendCtrlShiftV();
            return;
        }

        SendCtrlV();
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyControl, 0),
            CreateKeyboardInput(VirtualKeyV, 0),
            CreateKeyboardInput(VirtualKeyV, KeyEventKeyUp),
            CreateKeyboardInput(VirtualKeyControl, KeyEventKeyUp)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed to dispatch paste keys.");
        }
    }

    private static void SendCtrlShiftV()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyControl, 0),
            CreateKeyboardInput(VirtualKeyShift, 0),
            CreateKeyboardInput(VirtualKeyV, 0),
            CreateKeyboardInput(VirtualKeyV, KeyEventKeyUp),
            CreateKeyboardInput(VirtualKeyShift, KeyEventKeyUp),
            CreateKeyboardInput(VirtualKeyControl, KeyEventKeyUp)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed to dispatch paste keys.");
        }
    }

    private static string? GetProcessName(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            U = new InputUnion
            {
                KeyboardInput = new KeyboardInputData
                {
                    VirtualKey = virtualKey,
                    Flags = flags
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInputData MouseInput;

        [FieldOffset(0)]
        public KeyboardInputData KeyboardInput;

        [FieldOffset(0)]
        public HardwareInputData HardwareInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInputData
    {
        public uint Message;
        public ushort ParamLow;
        public ushort ParamHigh;
    }
}
