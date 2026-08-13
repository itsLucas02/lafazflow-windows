using System.Diagnostics;
using System.IO;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public interface IWhisperWorkerProcess : IDisposable
{
    int Id { get; }

    bool HasExited { get; }

    event EventHandler? Exited;

    void Start(string pipeName, AppSettings settings);

    void KillExact();

    bool WaitForExit(int timeoutMilliseconds);
}

public sealed class WhisperWorkerProcess : IWhisperWorkerProcess
{
    private readonly string _workerExecutablePath;
    private readonly bool _captureDiagnostics;
    private readonly string _diagnosticsDirectory;
    private Process? _process;

    public WhisperWorkerProcess(
        string workerExecutablePath,
        bool captureDiagnostics = false,
        string diagnosticsDirectory = "")
    {
        _workerExecutablePath = workerExecutablePath;
        _captureDiagnostics = captureDiagnostics;
        _diagnosticsDirectory = diagnosticsDirectory;
    }

    public int Id => _process?.Id ?? 0;

    public bool HasExited => _process?.HasExited ?? true;

    public event EventHandler? Exited;

    public void Start(string pipeName, AppSettings settings)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _workerExecutablePath,
            Arguments = BuildWorkerArguments(pipeName, settings),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(_workerExecutablePath)
                ?? Environment.CurrentDirectory
        };

        if (_captureDiagnostics)
        {
            Directory.CreateDirectory(_diagnosticsDirectory);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);
        if (!_process.Start())
        {
            throw new InvalidOperationException("Unable to start the Whisper worker process.");
        }

        if (_captureDiagnostics)
        {
            var stamp = Guid.NewGuid().ToString("N");
            _ = _process.StandardOutput.ReadToEndAsync().ContinueWith(task =>
            {
                try
                {
                    File.WriteAllText(Path.Combine(_diagnosticsDirectory, $"worker-{stamp}.out"), task.Result);
                }
                catch
                {
                }
            }, TaskScheduler.Default);
            _ = _process.StandardError.ReadToEndAsync().ContinueWith(task =>
            {
                try
                {
                    File.WriteAllText(Path.Combine(_diagnosticsDirectory, $"worker-{stamp}.err"), task.Result);
                }
                catch
                {
                }
            }, TaskScheduler.Default);
        }
    }

    public void KillExact()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill();
            }
        }
        catch
        {
        }
    }

    public bool WaitForExit(int timeoutMilliseconds)
    {
        return _process?.WaitForExit(timeoutMilliseconds) ?? true;
    }

    public void Dispose()
    {
        KillExact();
        _process?.Dispose();
        _process = null;
    }

    public static string BuildWorkerArguments(string pipeName, AppSettings settings)
    {
        var runtime = WhisperCliTranscriptionService.ResolveRuntime(settings);
        var prompt = WhisperPromptBuilder.BuildVocabularyPrompt(settings);
        var arguments = new List<string>
        {
            $"--pipe {Quote(pipeName)}",
            $"--model {Quote(runtime.ModelPath)}"
        };

        if (runtime.DecodeOptions.EnableVad)
        {
            arguments.Add($"--vad-model {Quote(runtime.DecodeOptions.VadModelPath)}");
        }

        arguments.Add($"--threads {Math.Clamp(settings.WhisperThreads, 1, Environment.ProcessorCount)}");
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            arguments.Add($"--prompt {Quote(prompt)}");
        }

        if (runtime.DecodeOptions.EnableVad)
        {
            arguments.Add("--vad-params vt=0.50,vspd=250,vsd=100,vp=30,vo=0.10");
        }

        if (settings.TranscriptionProfile != TranscriptionProfile.Quality
            || settings.WhisperBackend != WhisperBackend.Cuda)
        {
            arguments.Add("--cpu");
        }

        return string.Join(' ', arguments);
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
