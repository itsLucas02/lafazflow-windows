using System.Diagnostics;
using System.IO;
using LafazFlow.Windows.Core;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public enum RuntimeDiagnosticSeverity
{
    Ok,
    Warning,
    Error
}

public sealed record RuntimeDiagnosticRow(
    string Name,
    string Status,
    string Detail,
    RuntimeDiagnosticSeverity Severity);

public sealed record RuntimeSmokeCheckResult(bool Success, string Message);

public interface IRuntimeEnvironmentProbe
{
    bool FileExists(string path);

    int GetMicrophoneDeviceCount();

    bool CanWriteToDirectory(string path);

    Task<RuntimeSmokeCheckResult> RunWhisperSmokeCheckAsync(
        string whisperCliPath,
        string processPath,
        CancellationToken cancellationToken);
}

public sealed class RuntimeDiagnosticsService
{
    private readonly IRuntimeEnvironmentProbe _probe;

    public RuntimeDiagnosticsService()
        : this(new RuntimeEnvironmentProbe())
    {
    }

    public RuntimeDiagnosticsService(IRuntimeEnvironmentProbe probe)
    {
        _probe = probe;
    }

    public string BuildProfileStatus(AppSettings settings)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var profile = settings.TranscriptionProfile.ToString();
        var backend = settings.TranscriptionProfile == TranscriptionProfile.Quality
            ? FormatBackend(settings.WhisperBackend)
            : "CPU";
        var modelFileName = string.IsNullOrWhiteSpace(runtime.ModelPath)
            ? "No model selected"
            : Path.GetFileName(runtime.ModelPath);

        return $"{profile} / {backend} / {modelFileName}";
    }

    public IReadOnlyList<RuntimeDiagnosticRow> BuildDiagnostics(AppSettings settings, string logsFolder)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var rows = new List<RuntimeDiagnosticRow>
        {
            BuildFileRow("Whisper CLI", runtime.CliPath, "Selected local whisper-cli.exe is available."),
            BuildFileRow("Whisper model", runtime.ModelPath, "Selected local model file is available.")
        };

        if (settings.TranscriptionProfile == TranscriptionProfile.Quality
            && settings.WhisperBackend == WhisperBackend.Cuda)
        {
            rows.Add(BuildFileRow(
                "CUDA Whisper CLI",
                settings.CudaWhisperCliPath,
                "Selected CUDA whisper-cli.exe is available."));
        }

        if (settings.EnableVad)
        {
            rows.Add(BuildFileRow("VAD model", settings.VadModelPath, "Selected local VAD model is available."));
        }

        rows.Add(BuildMicrophoneRow());
        rows.Add(BuildLogsFolderRow(logsFolder));
        return rows;
    }

    public RuntimeDiagnosticRow TestMicrophone()
    {
        return BuildMicrophoneRow();
    }

    public async Task<RuntimeDiagnosticRow> TestTranscriptionSmokeAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var pathError = ValidateRuntimePaths(runtime);
        if (pathError is not null)
        {
            return new RuntimeDiagnosticRow(
                "Test transcription",
                "Error",
                pathError,
                RuntimeDiagnosticSeverity.Error);
        }

        var processPath = WhisperCliTranscriptionService.BuildProcessPath(
            runtime.CliPath,
            Environment.GetEnvironmentVariable("PATH") ?? "");
        var result = await _probe.RunWhisperSmokeCheckAsync(
            runtime.CliPath,
            processPath,
            cancellationToken);

        return result.Success
            ? new RuntimeDiagnosticRow(
                "Test transcription",
                "OK",
                result.Message,
                RuntimeDiagnosticSeverity.Ok)
            : new RuntimeDiagnosticRow(
                "Test transcription",
                "Error",
                result.Message,
                RuntimeDiagnosticSeverity.Error);
    }

    private RuntimeDiagnosticRow BuildFileRow(string name, string path, string okDetail)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new RuntimeDiagnosticRow(name, "Error", $"{name} path is empty.", RuntimeDiagnosticSeverity.Error);
        }

        return _probe.FileExists(path)
            ? new RuntimeDiagnosticRow(name, "OK", okDetail, RuntimeDiagnosticSeverity.Ok)
            : new RuntimeDiagnosticRow(name, "Error", $"{name} was not found: {path}", RuntimeDiagnosticSeverity.Error);
    }

    private string? ValidateRuntimePaths(WhisperRuntimeOptions runtime)
    {
        if (!_probe.FileExists(runtime.CliPath))
        {
            return "Whisper CLI was not found.";
        }

        if (!_probe.FileExists(runtime.ModelPath))
        {
            return "Whisper model was not found.";
        }

        if (runtime.DecodeOptions.EnableVad && !_probe.FileExists(runtime.DecodeOptions.VadModelPath))
        {
            return "VAD model was not found.";
        }

        return null;
    }

    private RuntimeDiagnosticRow BuildMicrophoneRow()
    {
        var deviceCount = _probe.GetMicrophoneDeviceCount();
        return deviceCount > 0
            ? new RuntimeDiagnosticRow("Microphone", "OK", $"{deviceCount} recording device(s) available.", RuntimeDiagnosticSeverity.Ok)
            : new RuntimeDiagnosticRow("Microphone", "Error", "No Windows recording device was detected.", RuntimeDiagnosticSeverity.Error);
    }

    private RuntimeDiagnosticRow BuildLogsFolderRow(string logsFolder)
    {
        return _probe.CanWriteToDirectory(logsFolder)
            ? new RuntimeDiagnosticRow("Logs folder", "OK", "Logs folder is writable.", RuntimeDiagnosticSeverity.Ok)
            : new RuntimeDiagnosticRow("Logs folder", "Error", $"Logs folder is not writable: {logsFolder}", RuntimeDiagnosticSeverity.Error);
    }

    private static string FormatBackend(WhisperBackend backend)
    {
        return backend == WhisperBackend.Cuda ? "CUDA" : "CPU";
    }
}

public sealed class RuntimeEnvironmentProbe : IRuntimeEnvironmentProbe
{
    private static readonly TimeSpan SmokeCheckTimeout = TimeSpan.FromSeconds(5);

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public int GetMicrophoneDeviceCount()
    {
        try
        {
            return WaveInEvent.DeviceCount;
        }
        catch
        {
            return 0;
        }
    }

    public bool CanWriteToDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, $".lafazflow-write-check-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<RuntimeSmokeCheckResult> RunWhisperSmokeCheckAsync(
        string whisperCliPath,
        string processPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SmokeCheckTimeout);
            var startInfo = new ProcessStartInfo
            {
                FileName = whisperCliPath,
                Arguments = "--help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(whisperCliPath) ?? Environment.CurrentDirectory
            };
            startInfo.Environment["PATH"] = processPath;

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new RuntimeSmokeCheckResult(false, "Unable to start Whisper CLI.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return new RuntimeSmokeCheckResult(false, "Whisper CLI smoke check timed out.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode == 0)
            {
                return new RuntimeSmokeCheckResult(true, "Whisper CLI started successfully.");
            }

            var message = FirstNonEmpty(stderr, stdout, $"Whisper CLI exited with code {process.ExitCode}.");
            return new RuntimeSmokeCheckResult(false, message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new RuntimeSmokeCheckResult(false, ex.Message);
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }

    private static void TryKill(Process process)
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
            // Best effort cleanup only.
        }
    }
}
