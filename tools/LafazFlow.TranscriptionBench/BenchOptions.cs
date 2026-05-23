namespace LafazFlow.TranscriptionBench;

public sealed record BenchOptions(
    string SettingsPath,
    string RecordingsDirectory,
    int Take,
    string OutputDirectory,
    IReadOnlySet<string>? ConfigFilter,
    string? PackName,
    string PacksRoot)
{
    public static BenchOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[index][2..];
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[key] = args[++index];
            }
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var settingsPath = values.GetValueOrDefault("settings")
            ?? Path.Combine(appData, "LafazFlow", "settings.json");
        var outputDirectory = values.GetValueOrDefault("out")
            ?? Path.Combine(localAppData, "LafazFlow", "Benchmarks");
        var take = int.TryParse(values.GetValueOrDefault("take"), out var parsedTake)
            ? Math.Max(1, parsedTake)
            : 20;
        var configFilter = values.TryGetValue("configs", out var configs) && !string.IsNullOrWhiteSpace(configs)
            ? configs
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        var packName = values.GetValueOrDefault("pack");
        var packsRoot = values.GetValueOrDefault("packs-root") ?? RegressionPackResolver.DefaultPacksRoot();
        var recordingsDirectory = !string.IsNullOrWhiteSpace(packName)
            ? RegressionPackResolver.Resolve(packName, packsRoot)
            : values.GetValueOrDefault("recordings")
                ?? Path.Combine(localAppData, "LafazFlow", "Recordings");

        return new BenchOptions(
            settingsPath,
            recordingsDirectory,
            take,
            outputDirectory,
            configFilter,
            string.IsNullOrWhiteSpace(packName) ? null : packName,
            packsRoot);
    }
}
