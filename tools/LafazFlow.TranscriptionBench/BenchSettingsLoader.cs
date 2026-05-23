using System.Text.Json;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.TranscriptionBench;

public static class BenchSettingsLoader
{
    public static AppSettings Load(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return new SettingsStore().Load();
        }

        if (string.Equals(Path.GetFileName(settingsPath), "settings.json", StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(settingsPath) is { } settingsRoot)
        {
            return new SettingsStore(settingsRoot).Load();
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new SettingsStore().Load();
    }
}
