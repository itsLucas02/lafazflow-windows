using System.Diagnostics;

namespace LafazFlow.Windows.Services;

public enum WhisperWorkload
{
    FinalTranscription,
    LivePreview,
    Diagnostic
}

public sealed record WhisperProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class WhisperProcessCoordinator
{
    public static WhisperProcessCoordinator Shared { get; } = new();

    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _processGate = new(1, 1);
    private readonly object _stateLock = new();
    private CancellationTokenSource? _activeInterruptibleCancellation;

    public async Task<WhisperProcessResult> RunAsync(
        WhisperWorkload workload,
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executionCancellation.CancelAfter(timeout);

        if (workload == WhisperWorkload.FinalTranscription)
        {
            CancelActiveInterruptibleWork();
        }

        await _processGate.WaitAsync(executionCancellation.Token);
        try
        {
            if (workload != WhisperWorkload.FinalTranscription)
            {
                lock (_stateLock)
                {
                    _activeInterruptibleCancellation = executionCancellation;
                }
            }

            return await RunProcessAsync(startInfo, executionCancellation.Token);
        }
        finally
        {
            if (workload != WhisperWorkload.FinalTranscription)
            {
                lock (_stateLock)
                {
                    if (ReferenceEquals(_activeInterruptibleCancellation, executionCancellation))
                    {
                        _activeInterruptibleCancellation = null;
                    }
                }
            }

            _processGate.Release();
        }
    }

    private void CancelActiveInterruptibleWork()
    {
        lock (_stateLock)
        {
            _activeInterruptibleCancellation?.Cancel();
        }
    }

    private static async Task<WhisperProcessResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start Whisper CLI.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new WhisperProcessResult(
                process.ExitCode,
                await stdoutTask,
                await stderrTask);
        }
        catch
        {
            TryKill(process);
            try
            {
                await process.WaitForExitAsync().WaitAsync(CleanupTimeout);
                await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(CleanupTimeout);
            }
            catch
            {
                // The process tree has already been terminated best-effort.
            }

            throw;
        }
        finally
        {
            TryKill(process);
        }
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
            // Best-effort cleanup must never hide the original process result.
        }
    }
}
