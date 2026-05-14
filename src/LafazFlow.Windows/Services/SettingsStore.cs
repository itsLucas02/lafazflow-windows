using System.IO;
using System.Text.Json;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class SettingsStore
{
    private readonly string _settingsPath;
    private readonly string _defaultWhisperCliPath;
    private readonly string _defaultModelPath;

    public SettingsStore(
        string? rootDirectory = null,
        string defaultWhisperCliPath = @"C:\Tools\whisper.cpp\Release\whisper-cli.exe",
        string defaultModelPath = @"C:\Models\whisper\ggml-base.en.bin")
    {
        _defaultWhisperCliPath = defaultWhisperCliPath;
        _defaultModelPath = defaultModelPath;
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LafazFlow");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return BuildDefaultSettings();
        }

        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions()) ?? AppSettings.Default;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var json = JsonSerializer.Serialize(settings, JsonOptions());
        File.WriteAllText(_settingsPath, json);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true
    };

    private AppSettings BuildDefaultSettings()
    {
        var settings = AppSettings.Default;

        if (File.Exists(_defaultWhisperCliPath))
        {
            settings = settings with { WhisperCliPath = _defaultWhisperCliPath };
        }

        if (File.Exists(_defaultModelPath))
        {
            settings = settings with { ModelPath = _defaultModelPath };
        }

        return settings;
    }
}
