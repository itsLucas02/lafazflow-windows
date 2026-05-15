using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LafazFlow.Windows.Services;

public sealed class DoubleShiftHotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;
    private static readonly TimeSpan DoublePressWindow = TimeSpan.FromMilliseconds(500);

    private readonly LowLevelKeyboardProc _proc;
    private readonly DoubleShiftDetector _detector = new(DoublePressWindow);
    private IntPtr _hookId;

    public event Action? DoubleShiftPressed;

    public DoubleShiftHotkeyService()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        _hookId = SetHook(_proc);
        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to install double Shift keyboard hook.");
        }
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var virtualKey = Marshal.ReadInt32(lParam);
            if (IsShift(virtualKey) && IsKeyDownMessage(wParam))
            {
                var flags = Marshal.ReadInt32(lParam, 8);
                var isRepeat = (flags & 0x40000000) != 0;
                if (_detector.RegisterKeyDown(DateTimeOffset.UtcNow, isRepeat))
                {
                    DoubleShiftPressed?.Invoke();
                }
            }
            else if (IsShift(virtualKey) && IsKeyUpMessage(wParam))
            {
                _detector.RegisterKeyUp();
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsShift(int virtualKey)
    {
        return virtualKey is VkShift or VkLeftShift or VkRightShift;
    }

    private static bool IsKeyDownMessage(IntPtr message)
    {
        return message == WmKeyDown || message == WmSysKeyDown;
    }

    private static bool IsKeyUpMessage(IntPtr message)
    {
        return message == WmKeyUp || message == WmSysKeyUp;
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = GetModuleHandle(currentModule?.ModuleName);
        return SetWindowsHookEx(WhKeyboardLl, proc, moduleHandle, 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
