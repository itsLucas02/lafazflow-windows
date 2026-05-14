using System.Reflection;
using System.Runtime.InteropServices;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class ClipboardPasteServiceNativeInteropTests
{
    [Fact]
    public void InputStructureMatchesWin32Size()
    {
        var inputType = typeof(ClipboardPasteService).GetNestedType("Input", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Input type was not found.");

        var expectedSize = Environment.Is64BitProcess ? 40 : 28;

        Assert.Equal(expectedSize, Marshal.SizeOf(inputType));
    }
}
