using LafazFlow.TranscriptionBench;

var options = BenchOptions.Parse(args);
var settings = BenchSettingsLoader.Load(options.SettingsPath);
var fixtures = RecordingFixtureDiscovery.Discover(options.RecordingsDirectory, options.Take);
if (fixtures.Count == 0)
{
    Console.Error.WriteLine($"No benchmark fixtures found in {options.RecordingsDirectory}.");
    Console.Error.WriteLine("Expected .wav files with matching .txt transcripts.");
    return 2;
}

var configs = BenchmarkConfigFactory.Build(settings, options.ConfigFilter);
if (configs.Count == 0)
{
    Console.Error.WriteLine("No benchmark configs selected.");
    return 2;
}

Console.WriteLine($"Running {fixtures.Count} fixture(s) across {configs.Count} config(s).");
var runner = new BenchmarkRunner();
var results = await runner.RunAsync(fixtures, configs, CancellationToken.None);
var (markdownPath, csvPath) = BenchmarkReportWriter.Write(options.OutputDirectory, results, DateTimeOffset.Now);

Console.WriteLine($"Markdown report: {markdownPath}");
Console.WriteLine($"CSV report: {csvPath}");

return results.Any(result => result.Succeeded) ? 0 : 1;

internal sealed record BenchOptions(
    string SettingsPath,
    string RecordingsDirectory,
    int Take,
    string OutputDirectory,
    IReadOnlySet<string>? ConfigFilter)
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
        var recordingsDirectory = values.GetValueOrDefault("recordings")
            ?? Path.Combine(localAppData, "LafazFlow", "Recordings");
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

        return new BenchOptions(settingsPath, recordingsDirectory, take, outputDirectory, configFilter);
    }
}
