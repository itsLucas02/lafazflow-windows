using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenFileDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);

        var settings = store.Load();

        Assert.Equal("DoubleShift", settings.HotkeyGesture);
        Assert.Equal(HotkeyMode.Hybrid, settings.HotkeyMode);
        Assert.True(settings.RestoreClipboardAfterPaste);
        Assert.Equal(250, settings.ClipboardRestoreDelayMs);
        Assert.False(settings.AppendTrailingSpace);
        Assert.False(settings.KeepRecordingsForDiagnostics);
    }

    [Fact]
    public void SaveThenLoadRoundTripsSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var expected = AppSettings.Default with
        {
            HotkeyGesture = "Ctrl+Shift+D",
            HotkeyMode = HotkeyMode.Toggle,
            WhisperCliPath = @"C:\Tools\whisper-cli.exe",
            ModelPath = @"C:\Models\ggml-base.en.bin",
            AppendTrailingSpace = true
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected, actual);
    }
}
