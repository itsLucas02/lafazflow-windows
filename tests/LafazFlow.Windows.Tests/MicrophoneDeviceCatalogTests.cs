using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class MicrophoneDeviceCatalogTests
{
    [Fact]
    public void ResolveIndexMatchesDeviceNameCaseInsensitively()
    {
        var devices = new[]
        {
            new MicrophoneDeviceInfo(0, "Microphone Array"),
            new MicrophoneDeviceInfo(2, "Headset Microphone")
        };

        Assert.Equal(2, MicrophoneDeviceCatalog.ResolveIndex("headset microphone", devices));
        Assert.Equal(0, MicrophoneDeviceCatalog.ResolveIndex("MICROPHONE ARRAY", devices));
    }

    [Fact]
    public void ResolveIndexReturnsNullForEmptyOrUnknownName()
    {
        var devices = new[] { new MicrophoneDeviceInfo(0, "Microphone Array") };

        Assert.Null(MicrophoneDeviceCatalog.ResolveIndex(null, devices));
        Assert.Null(MicrophoneDeviceCatalog.ResolveIndex("", devices));
        Assert.Null(MicrophoneDeviceCatalog.ResolveIndex("Unknown Mic", devices));
    }
}
