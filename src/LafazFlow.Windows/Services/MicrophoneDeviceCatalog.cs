using NAudio.CoreAudioApi;

namespace LafazFlow.Windows.Services;

public sealed record MicrophoneDeviceInfo(int Index, string Name);

/// <summary>
/// Enumerates active Windows audio endpoints and
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
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            for (var index = 0; index < endpoints.Count; index++)
            {
                var name = endpoints[index].FriendlyName?.Trim();
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

        return devices.FirstOrDefault(device => string.Equals(device.Name, deviceName, StringComparison.OrdinalIgnoreCase))?.Index
            ?? devices.FirstOrDefault(device => device.Name.StartsWith(deviceName, StringComparison.OrdinalIgnoreCase))?.Index;
    }
}
