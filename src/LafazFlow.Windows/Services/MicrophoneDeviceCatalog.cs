using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed record MicrophoneDeviceInfo(int Index, string Name);

/// <summary>
/// Enumerates the Windows input devices visible to the NAudio waveIn API and
/// resolves a persisted device name back to its device index. Recording always
/// binds to a concrete device so a changed Windows default cannot silently
/// capture from the wrong microphone.
/// </summary>
public static class MicrophoneDeviceCatalog
{
    public static IReadOnlyList<MicrophoneDeviceInfo> ListDevices()
    {
        var devices = new List<MicrophoneDeviceInfo>();
        try
        {
            var count = WaveInEvent.DeviceCount;
            for (var index = 0; index < count; index++)
            {
                var capabilities = WaveInEvent.GetCapabilities(index);
                var name = capabilities.ProductName?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    devices.Add(new MicrophoneDeviceInfo(index, name));
                }
            }
        }
        catch
        {
            // No usable input devices; the caller falls back to the default.
        }

        return devices;
    }

    public static int? ResolveIndex(string? deviceName)
    {
        return ResolveIndex(deviceName, ListDevices());
    }

    public static int? ResolveIndex(string? deviceName, IReadOnlyList<MicrophoneDeviceInfo> devices)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        return devices
            .FirstOrDefault(device => string.Equals(device.Name, deviceName, StringComparison.OrdinalIgnoreCase))
            ?.Index;
    }
}
