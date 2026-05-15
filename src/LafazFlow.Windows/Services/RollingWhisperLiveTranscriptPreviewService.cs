using System.Diagnostics;
using System.IO;
using LafazFlow.Windows.Core;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed class RollingWhisperLiveTranscriptPreviewService : ILiveTranscriptPreviewService
{
    private const int SampleRate = 16000;
    private const int BytesPerSample = 2;
    private const int PreviewIntervalMilliseconds = 1400;
    private const int MinimumAudioMilliseconds = 1400;
    private const int RollingWindowMilliseconds = 8000;
    private const int MinimumBytes = SampleRate * BytesPerSample * MinimumAudioMilliseconds / 1000;
    private const int RollingWindowBytes = SampleRate * BytesPerSample * RollingWindowMilliseconds / 1000;

    private readonly object _lock = new();
    private readonly List<byte> _audioBuffer = [];
    private CancellationTokenSource? _sessionCancellation;
    private Task? _previewLoop;
    private AppSettings? _settings;
    private Action<string>? _onPartialTranscript;
    private string _lastPreview = "";

    public Task StartAsync(
        AppSettings settings,
        Action<string> onPartialTranscript,
        CancellationToken cancellationToken)
    {
        StopAsync().GetAwaiter().GetResult();

        _settings = settings;
        _onPartialTranscript = onPartialTranscript;
        _lastPreview = "";
        lock (_lock)
        {
            _audioBuffer.Clear();
        }

        _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _previewLoop = Task.Run(() => RunPreviewLoopAsync(_sessionCancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public void AcceptAudioChunk(byte[] audioChunk)
    {
        if (_sessionCancellation is null || audioChunk.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            _audioBuffer.AddRange(audioChunk);
            if (_audioBuffer.Count > RollingWindowBytes)
            {
                _audioBuffer.RemoveRange(0, _audioBuffer.Count - RollingWindowBytes);
            }
        }
    }

    public async Task StopAsync()
    {
        var cancellation = _sessionCancellation;
        var loop = _previewLoop;

        _sessionCancellation = null;
        _previewLoop = null;
        _settings = null;
        _onPartialTranscript = null;
        _lastPreview = "";
        lock (_lock)
        {
            _audioBuffer.Clear();
        }

        if (cancellation is null)
        {
            return;
        }

        await cancellation.CancelAsync();
        try
        {
            if (loop is not null)
            {
                await loop;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task RunPreviewLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(PreviewIntervalMilliseconds, cancellationToken);
            var snapshot = SnapshotAudio();
            if (snapshot.Length < MinimumBytes)
            {
                continue;
            }

            var preview = await TranscribeSnapshotAsync(snapshot, cancellationToken);
            if (string.IsNullOrWhiteSpace(preview) || string.Equals(preview, _lastPreview, StringComparison.Ordinal))
            {
                continue;
            }

            _lastPreview = preview;
            _onPartialTranscript?.Invoke(preview);
        }
    }

    private byte[] SnapshotAudio()
    {
        lock (_lock)
        {
            return _audioBuffer.ToArray();
        }
    }

    private async Task<string> TranscribeSnapshotAsync(byte[] pcmAudio, CancellationToken cancellationToken)
    {
        var settings = _settings ?? throw new OperationCanceledException();
        if (WhisperCliTranscriptionService.ValidatePaths(settings.WhisperCliPath, settings.ModelPath) is not null)
        {
            return "";
        }

        var previewRoot = Path.Combine(
            Path.GetTempPath(),
            "LafazFlow",
            "LivePreview");
        Directory.CreateDirectory(previewRoot);

        var audioPath = Path.Combine(previewRoot, $"{Guid.NewGuid():N}.wav");
        try
        {
            await WriteWavAsync(audioPath, pcmAudio, cancellationToken);
            var threads = Math.Clamp(Math.Max(1, settings.WhisperThreads / 2), 1, Environment.ProcessorCount);
            var transcript = await RunWhisperAsync(settings, audioPath, threads, cancellationToken);
            if (settings.EnableVocabularyCorrections)
            {
                transcript = VocabularyCorrectionService.ApplyDefaults(transcript);
            }

            return transcript.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return "";
        }
        finally
        {
            TryDelete(audioPath);
            TryDelete(Path.ChangeExtension(audioPath, ".txt"));
        }
    }

    private static async Task WriteWavAsync(string audioPath, byte[] pcmAudio, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(audioPath);
        await using var writer = new WaveFileWriter(stream, new WaveFormat(SampleRate, 16, 1));
        await writer.WriteAsync(pcmAudio, cancellationToken);
    }

    private static async Task<string> RunWhisperAsync(
        AppSettings settings,
        string audioPath,
        int threads,
        CancellationToken cancellationToken)
    {
        var outputBasePath = Path.Combine(
            Path.GetDirectoryName(audioPath)!,
            Path.GetFileNameWithoutExtension(audioPath));
        var startInfo = new ProcessStartInfo
        {
            FileName = settings.WhisperCliPath,
            Arguments = WhisperCliTranscriptionService.BuildArguments(
                settings.ModelPath,
                audioPath,
                outputBasePath,
                settings.WhisperInitialPrompt,
                threads),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(settings.WhisperCliPath) ?? Environment.CurrentDirectory
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return "";
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);
        if (process.ExitCode != 0)
        {
            return "";
        }

        var textPath = outputBasePath + ".txt";
        return File.Exists(textPath)
            ? WhisperCliTranscriptionService.CleanTranscript(await File.ReadAllTextAsync(textPath, cancellationToken))
            : WhisperCliTranscriptionService.CleanTranscript(await stdoutTask);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
