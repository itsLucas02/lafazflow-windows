using System.Diagnostics;
using System.IO;

namespace LafazFlow.Windows.Services;

public sealed class WhisperCliTranscriptionService
{
    public static string? ValidatePaths(string whisperCliPath, string modelPath)
    {
        if (!File.Exists(whisperCliPath))
        {
            return "Whisper CLI was not found.";
        }

        if (!File.Exists(modelPath))
        {
            return "Whisper model was not found.";
        }

        return null;
    }

    public static string BuildArguments(string modelPath, string audioPath, string outputBasePath)
    {
        return $"-m {Quote(modelPath)} -f {Quote(audioPath)} -otxt -of {Quote(outputBasePath)}";
    }

    public async Task<string> TranscribeAsync(
        string whisperCliPath,
        string modelPath,
        string audioPath,
        CancellationToken cancellationToken)
    {
        var pathError = ValidatePaths(whisperCliPath, modelPath);
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
            Arguments = BuildArguments(modelPath, audioPath, outputBasePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(whisperCliPath) ?? Environment.CurrentDirectory
        };

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
        return text.Trim();
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
