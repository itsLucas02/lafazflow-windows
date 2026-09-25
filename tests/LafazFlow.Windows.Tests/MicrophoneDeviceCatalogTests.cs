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

    [Fact]
    public void ExactEndpointNameWinsOverLegacyTruncatedPrefix()
    {
        var devices = new[]
        {
            new MicrophoneDeviceInfo(0, "Microphone Pro"),
            new MicrophoneDeviceInfo(1, "Microphone")
        };

        Assert.Equal(1, MicrophoneDeviceCatalog.ResolveIndex("Microphone", devices));
        Assert.Equal(0, MicrophoneDeviceCatalog.ResolveIndex("Microphone P", devices));
    }
}
