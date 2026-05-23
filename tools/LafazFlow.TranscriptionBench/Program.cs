using LafazFlow.TranscriptionBench;

var options = BenchOptions.Parse(args);
var settings = BenchSettingsLoader.Load(options.SettingsPath);
var fixtures = RecordingFixtureDiscovery.Discover(options.RecordingsDirectory, options.Take);
if (fixtures.Count == 0)
{
    Console.Error.WriteLine($"No benchmark fixtures found in {options.RecordingsDirectory}.");
    Console.Error.WriteLine("Expected .wav files with matching .txt transcripts.");
    if (!string.IsNullOrWhiteSpace(options.PackName))
    {
        Console.Error.WriteLine($"For pack '{options.PackName}', place private fixture pairs in:");
        Console.Error.WriteLine(options.RecordingsDirectory);
        Console.Error.WriteLine("Example: stripe-checkout.wav + stripe-checkout.txt");
    }

    return 2;
}

var configs = BenchmarkConfigFactory.Build(settings, options.ConfigFilter);
if (configs.Count == 0)
{
    Console.Error.WriteLine("No benchmark configs selected.");
    return 2;
}

Console.WriteLine($"Running {fixtures.Count} fixture(s) across {configs.Count} config(s).");
if (!string.IsNullOrWhiteSpace(options.PackName))
{
    Console.WriteLine($"Regression pack: {options.PackName}");
}

var runner = new BenchmarkRunner();
var results = await runner.RunAsync(fixtures, configs, CancellationToken.None);
var (markdownPath, csvPath) = BenchmarkReportWriter.Write(options.OutputDirectory, results, DateTimeOffset.Now);

Console.WriteLine($"Markdown report: {markdownPath}");
Console.WriteLine($"CSV report: {csvPath}");

return results.Any(result => result.Succeeded) ? 0 : 1;
