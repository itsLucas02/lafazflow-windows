using System.Diagnostics;
using System.IO;
using LafazFlow.Windows.Core;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public sealed class RollingWhisperLiveTranscriptPreviewService : ILiveTranscriptPreviewService
{
    private const int SampleRate = 16000;
    private const int BytesPerSample = 2;

    private readonly object _lock = new();
    private readonly List<byte> _audioBuffer = [];
    private readonly LiveTranscriptStabilizer _stabilizer = new();
    private readonly RollingWhisperLiveTranscriptPreviewOptions _options;
    private readonly Func<AppSettings, byte[], int, CancellationToken, Task<string>> _transcribeSnapshotAsync;
    private readonly Action<string> _logMessage;
    private readonly int _minimumBytes;
    private readonly int _rollingWindowBytes;
    private readonly int _minimumNewAudioBytes;
    private CancellationTokenSource? _sessionCancellation;
    private Task? _previewLoop;
    private AppSettings? _settings;
    private Action<string>? _onPartialTranscript;
    private string _lastPreview = "";
    private long _totalAudioByteCount;
    private long _lastAttemptTotalAudioByteCount;
    private PreviewSessionStats _stats = new();

    public RollingWhisperLiveTranscriptPreviewService()
        : this(new RollingWhisperLiveTranscriptPreviewOptions(), null, null)
    {
    }

    public RollingWhisperLiveTranscriptPreviewService(
        RollingWhisperLiveTranscriptPreviewOptions options,
        Func<AppSettings, byte[], int, CancellationToken, Task<string>>? transcribeSnapshotAsync = null,
        Action<string>? logMessage = null)
    {
        _options = options;
        _transcribeSnapshotAsync = transcribeSnapshotAsync ?? DefaultTranscribeSnapshotAsync;
        _logMessage = logMessage ?? Log;
        _minimumBytes = MillisecondsToPcmBytes(options.MinimumAudioMilliseconds);
        _rollingWindowBytes = MillisecondsToPcmBytes(options.RollingWindowMilliseconds);
        _minimumNewAudioBytes = MillisecondsToPcmBytes(options.MinimumNewAudioMilliseconds);
    }

    public Task StartAsync(
        AppSettings settings,
        Action<string> onPartialTranscript,
        CancellationToken cancellationToken)
    {
        StopAsync().GetAwaiter().GetResult();

        _settings = settings;
        _onPartialTranscript = onPartialTranscript;
        _lastPreview = "";
        _totalAudioByteCount = 0;
        _lastAttemptTotalAudioByteCount = 0;
        _stats = new PreviewSessionStats();
        _stabilizer.Reset();
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
            _totalAudioByteCount += audioChunk.Length;
            _audioBuffer.AddRange(audioChunk);
            if (_audioBuffer.Count > _rollingWindowBytes)
            {
                _audioBuffer.RemoveRange(0, _audioBuffer.Count - _rollingWindowBytes);
            }
        }
    }

    public async Task StopAsync()
    {
        var cancellation = _sessionCancellation;
        var loop = _previewLoop;
        var stats = _stats;

        _sessionCancellation = null;
        _previewLoop = null;
        _settings = null;
        _onPartialTranscript = null;
        _lastPreview = "";
        _totalAudioByteCount = 0;
        _lastAttemptTotalAudioByteCount = 0;
        _stats = new PreviewSessionStats();
        _stabilizer.Reset();
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
            LogPreviewSummary(stats);
            cancellation.Dispose();
        }
    }

    private async Task RunPreviewLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_options.PreviewIntervalMilliseconds, cancellationToken);
            var snapshot = SnapshotAudio();
            if (snapshot.Audio.Length < _minimumBytes
                || snapshot.TotalAudioBytes - _lastAttemptTotalAudioByteCount < _minimumNewAudioBytes)
            {
                continue;
            }

            _lastAttemptTotalAudioByteCount = snapshot.TotalAudioBytes;
            _stats.Attempted++;
            var settings = _settings ?? throw new OperationCanceledException();
            var threads = Math.Clamp(Math.Max(1, settings.WhisperThreads / 2), 1, Environment.ProcessorCount);
            var preview = await _transcribeSnapshotAsync(settings, snapshot.Audio, threads, cancellationToken);
            if (!_stabilizer.TryAccept(preview, out var stablePreview))
            {
                _stats.CountSuppression(_stabilizer.LastSuppressionReason);
                continue;
            }

            if (string.Equals(stablePreview, _lastPreview, StringComparison.Ordinal))
            {
                _stats.Duplicate++;
                continue;
            }

            _lastPreview = stablePreview;
            _stats.Accepted++;
            _onPartialTranscript?.Invoke(stablePreview);
        }
    }

    private AudioSnapshot SnapshotAudio()
    {
        lock (_lock)
        {
            return new AudioSnapshot(_audioBuffer.ToArray(), _totalAudioByteCount);
        }
    }

    private async Task<string> DefaultTranscribeSnapshotAsync(
        AppSettings settings,
        byte[] pcmAudio,
        int threads,
        CancellationToken cancellationToken)
    {
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

    private void LogPreviewSummary(PreviewSessionStats stats)
    {
        if (stats.Attempted == 0)
        {
            return;
        }

        _logMessage(
            $"Live preview summary: attempted={stats.Attempted} accepted={stats.Accepted} duplicate={stats.Duplicate} regressive={stats.Regressive} empty={stats.Empty}.");
    }

    private static void Log(string message)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LafazFlow",
                "Logs");
            Directory.CreateDirectory(logRoot);
            File.AppendAllText(
                Path.Combine(logRoot, "lafazflow.log"),
                $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static int MillisecondsToPcmBytes(int milliseconds)
    {
        return SampleRate * BytesPerSample * milliseconds / 1000;
    }

    private sealed class PreviewSessionStats
    {
        public int Attempted { get; set; }

        public int Accepted { get; set; }

        public int Duplicate { get; set; }

        public int Regressive { get; set; }

        public int Empty { get; set; }

        public void CountSuppression(string reason)
        {
            switch (reason)
            {
                case "duplicate":
                    Duplicate++;
                    break;
                case "regressive":
                    Regressive++;
                    break;
                default:
                    Empty++;
                    break;
            }
        }
    }

    private sealed record AudioSnapshot(byte[] Audio, long TotalAudioBytes);
}
