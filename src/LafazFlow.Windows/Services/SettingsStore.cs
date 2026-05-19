using System.IO;
using System.Text.Json;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class SettingsStore
{
    private const string PreviousDefaultWhisperInitialPrompt =
        "Supabase, Vercel, Tailscale, Netlify, Mintlify, GitHub, PowerShell, Cursor, LafazFlow, Luqman.";
    private const string DeveloperDefaultWhisperInitialPrompt =
        "Supabase, Vercel, Tailscale, Netlify, Mintlify, GitHub, PowerShell, Cursor, LafazFlow, Luqman, "
        + "shadcn, shadcn/ui, shadcn-ui, components.json, Radix UI, Tailwind CSS, FieldGroup, InputGroup, "
        + "npx shadcn@latest, build-web-apps:shadcn.";

    private static readonly string[] ModelPriority =
    [
        "ggml-base.en.bin",
        "ggml-small.en.bin",
        "ggml-medium.en.bin",
        "ggml-large-v3-turbo-q5_0.bin",
        "ggml-large-v3-turbo.bin"
    ];

    private readonly string _settingsPath;
    private readonly string _defaultWhisperCliPath;
    private readonly string _defaultCudaWhisperCliPath;
    private readonly string _defaultModelPath;
    private readonly string _defaultQualityModelPath;
    private readonly string _defaultModelDirectory;

    public SettingsStore(
        string? rootDirectory = null,
        string defaultWhisperCliPath = @"C:\Tools\whisper.cpp\Release\whisper-cli.exe",
        string defaultModelPath = @"C:\Models\whisper\ggml-base.en.bin",
        string? defaultModelDirectory = null,
        string defaultCudaWhisperCliPath = @"C:\Tools\whisper.cpp-cuda\bin\whisper-cli.exe",
        string defaultQualityModelPath = @"C:\Models\whisper\ggml-large-v3-turbo-q5_0.bin")
    {
        _defaultWhisperCliPath = defaultWhisperCliPath;
        _defaultCudaWhisperCliPath = defaultCudaWhisperCliPath;
        _defaultModelPath = defaultModelPath;
        _defaultQualityModelPath = defaultQualityModelPath;
        _defaultModelDirectory = defaultModelDirectory
            ?? Path.GetDirectoryName(defaultModelPath)
            ?? @"C:\Models\whisper";
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
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions()) ?? AppSettings.Default;
        return Migrate(settings);
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

        if (File.Exists(_defaultCudaWhisperCliPath))
        {
            settings = settings with { CudaWhisperCliPath = _defaultCudaWhisperCliPath };
        }

        var modelPath = DetectBestModelPath();
        if (modelPath is not null)
        {
            settings = settings with { ModelPath = modelPath };
        }

        var qualityModelPath = DetectQualityModelPath();
        if (qualityModelPath is not null)
        {
            settings = settings with { QualityModelPath = qualityModelPath };
        }

        return settings;
    }

    private AppSettings Migrate(AppSettings settings)
    {
        if (settings.SettingsSchemaVersion >= AppSettings.CurrentSchemaVersion)
        {
            return settings;
        }

        var migrated = settings;

        if (migrated.ClipboardRestoreDelayMs <= 250)
        {
            migrated = migrated with { ClipboardRestoreDelayMs = AppSettings.DefaultClipboardRestoreDelayMs };
        }

        if (string.IsNullOrWhiteSpace(migrated.WhisperInitialPrompt)
            || string.Equals(migrated.WhisperInitialPrompt, PreviousDefaultWhisperInitialPrompt, StringComparison.Ordinal)
            || string.Equals(migrated.WhisperInitialPrompt, DeveloperDefaultWhisperInitialPrompt, StringComparison.Ordinal))
        {
            migrated = migrated with { WhisperInitialPrompt = AppSettings.DefaultWhisperInitialPrompt };
        }

        if (migrated.WhisperThreads <= 0)
        {
            migrated = migrated with { WhisperThreads = AppSettings.Default.WhisperThreads };
        }

        if (ShouldUpgradeDefaultModel(migrated.ModelPath))
        {
            var modelPath = DetectBestModelPath();
            if (modelPath is not null)
            {
                migrated = migrated with { ModelPath = modelPath };
            }
        }

        if (string.IsNullOrWhiteSpace(migrated.QualityModelPath))
        {
            migrated = migrated with { QualityModelPath = DetectQualityModelPath() ?? _defaultQualityModelPath };
        }

        if (string.IsNullOrWhiteSpace(migrated.CudaWhisperCliPath))
        {
            migrated = migrated with { CudaWhisperCliPath = _defaultCudaWhisperCliPath };
        }

        if (string.IsNullOrWhiteSpace(migrated.VadModelPath))
        {
            migrated = migrated with { VadModelPath = AppSettings.Default.VadModelPath };
        }

        return migrated with { SettingsSchemaVersion = AppSettings.CurrentSchemaVersion };
    }

    private bool ShouldUpgradeDefaultModel(string modelPath)
    {
        return string.IsNullOrWhiteSpace(modelPath)
            || string.Equals(modelPath, _defaultModelPath, StringComparison.OrdinalIgnoreCase);
    }

    private string? DetectBestModelPath()
    {
        foreach (var modelFileName in ModelPriority)
        {
            var candidate = Path.Combine(_defaultModelDirectory, modelFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return File.Exists(_defaultModelPath) ? _defaultModelPath : null;
    }

    private string? DetectQualityModelPath()
    {
        var candidate = Path.Combine(_defaultModelDirectory, "ggml-large-v3-turbo-q5_0.bin");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return File.Exists(_defaultQualityModelPath)
            ? _defaultQualityModelPath
            : null;
    }
}
