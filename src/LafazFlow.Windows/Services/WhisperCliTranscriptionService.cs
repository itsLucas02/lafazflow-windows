using System.Diagnostics;
using System.IO;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed class WhisperCliTranscriptionService : ITranscriptionService
{
    public static string? ValidatePaths(string whisperCliPath, string modelPath)
    {
        return ValidatePaths(whisperCliPath, modelPath, WhisperDecodeOptions.Fast);
    }

    public static string? ValidatePaths(string whisperCliPath, string modelPath, WhisperDecodeOptions decodeOptions)
    {
        if (!File.Exists(whisperCliPath))
        {
            return "Whisper CLI was not found.";
        }

        if (!File.Exists(modelPath))
        {
            return "Whisper model was not found.";
        }

        if (decodeOptions.EnableVad && !File.Exists(decodeOptions.VadModelPath))
        {
            return "VAD model was not found.";
        }

        return null;
    }

    public static string BuildArguments(
        string modelPath,
        string audioPath,
        string outputBasePath,
        string initialPrompt,
        int threads)
    {
        return BuildArguments(modelPath, audioPath, outputBasePath, initialPrompt, threads, WhisperDecodeOptions.Fast);
    }

    public static string BuildArguments(
        string modelPath,
        string audioPath,
        string outputBasePath,
        string initialPrompt,
        int threads,
        WhisperDecodeOptions decodeOptions)
    {
        var promptArgs = string.IsNullOrWhiteSpace(initialPrompt)
            ? ""
            : $" --prompt {Quote(initialPrompt)} --carry-initial-prompt";
        var safeThreads = Math.Clamp(threads, 1, Environment.ProcessorCount);
        var nonSpeechArgs = decodeOptions.SuppressNonSpeechTokens ? " -sns" : "";
        var vadArgs = decodeOptions.EnableVad
            ? $" --vad -vm {Quote(decodeOptions.VadModelPath)} -vt 0.50 -vspd 250 -vsd 100 -vp 30 -vo 0.10"
            : "";

        return $"-m {Quote(modelPath)} -f {Quote(audioPath)} -t {safeThreads} -otxt -nt -l en -tp {FormatTemperature(decodeOptions.Temperature)}{nonSpeechArgs}{vadArgs}{promptArgs} -of {Quote(outputBasePath)}";
    }

    public static WhisperRuntimeOptions ResolveRuntime(AppSettings settings)
    {
        var useQuality = settings.TranscriptionProfile == TranscriptionProfile.Quality;
        var cliPath = useQuality && settings.WhisperBackend == WhisperBackend.Cuda
            ? settings.CudaWhisperCliPath
            : settings.WhisperCliPath;
        var modelPath = useQuality ? settings.QualityModelPath : settings.ModelPath;
        var decodeOptions = useQuality && settings.EnableVad
            ? WhisperDecodeOptions.QualityWithVad(settings.VadModelPath)
            : WhisperDecodeOptions.Fast;

        if (useQuality && !settings.EnableVad)
        {
            decodeOptions = decodeOptions with
            {
                Temperature = 0.2,
                SuppressNonSpeechTokens = true
            };
        }

        return new WhisperRuntimeOptions(cliPath, modelPath, decodeOptions);
    }

    public async Task<string> TranscribeAsync(
        string whisperCliPath,
        string modelPath,
        string audioPath,
        string initialPrompt,
        int threads,
        WhisperDecodeOptions decodeOptions,
        CancellationToken cancellationToken)
    {
        var pathError = ValidatePaths(whisperCliPath, modelPath, decodeOptions);
        if (pathError is not null)
        {
            throw new InvalidOperationException(pathError);
        }

        if (!File.Exists(audioPath))
        {
            throw new InvalidOperationException("Audio file was not found.");
        }

        var outputBasePath = Path.Combine(
            Path.GetDirectoryName(audioPath)!,
            Path.GetFileNameWithoutExtension(audioPath));

        var startInfo = new ProcessStartInfo
        {
            FileName = whisperCliPath,
            Arguments = BuildArguments(modelPath, audioPath, outputBasePath, initialPrompt, threads, decodeOptions),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(whisperCliPath) ?? Environment.CurrentDirectory
        };
        startInfo.Environment["PATH"] = BuildProcessPath(
            whisperCliPath,
            Environment.GetEnvironmentVariable("PATH") ?? "");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start Whisper CLI.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            throw new InvalidOperationException($"Whisper CLI failed: {stderr.Trim()}");
        }

        var textPath = outputBasePath + ".txt";
        if (File.Exists(textPath))
        {
            return CleanTranscript(await File.ReadAllTextAsync(textPath, cancellationToken));
        }

        return CleanTranscript(await stdoutTask);
    }

    public static string CleanTranscript(string text)
    {
        return TranscriptionTextFormatter.Format(text);
    }

    public static string BuildProcessPath(string whisperCliPath, string existingPath)
    {
        return BuildProcessPath(whisperCliPath, existingPath, GetCudaRuntimeDirectories());
    }

    public static string BuildProcessPath(
        string whisperCliPath,
        string existingPath,
        IEnumerable<string> cudaRuntimeDirectories)
    {
        var entries = new List<string>();
        var cliDirectory = Path.GetDirectoryName(whisperCliPath);
        if (!string.IsNullOrWhiteSpace(cliDirectory))
        {
            entries.Add(cliDirectory);
        }

        entries.AddRange(cudaRuntimeDirectories.Where(Directory.Exists));

        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            entries.AddRange(existingPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        return string.Join(
            Path.PathSeparator,
            entries
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetCudaRuntimeDirectories()
    {
        var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
        if (!string.IsNullOrWhiteSpace(cudaPath))
        {
            yield return Path.Combine(cudaPath, "bin", "x64");
            yield return Path.Combine(cudaPath, "bin");
        }

        const string cudaRoot = @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA";
        if (!Directory.Exists(cudaRoot))
        {
            yield break;
        }

        foreach (var versionDirectory in Directory
            .GetDirectories(cudaRoot, "v*")
            .OrderByDescending(directory => directory, StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine(versionDirectory, "bin", "x64");
            yield return Path.Combine(versionDirectory, "bin");
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static string FormatTemperature(double temperature)
    {
        return temperature.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
