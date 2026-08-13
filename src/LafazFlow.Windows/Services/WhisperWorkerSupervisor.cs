using System.IO;
using System.IO.Pipes;
using System.Text;
using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public enum WhisperWorkerState
{
    Idle,
    Starting,
    Loading,
    Ready,
    Recovering,
    Unavailable
}

public sealed class WhisperWorkerSupervisorOptions
{
    public string WorkerExecutablePath { get; init; } = @"C:\Tools\lafazflow-whisper-worker\bin\lafazflow-whisper-worker.exe";

    public bool CaptureWorkerDiagnostics { get; init; }

    public string WorkerDiagnosticsDirectory { get; init; } =
        Path.Combine(Path.GetTempPath(), "LafazFlowWorkerDiag");

    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed class WhisperWorkerSupervisor : IDisposable
{
    private readonly WhisperWorkerSupervisorOptions _options;
    private readonly Func<IWhisperWorkerProcess> _processFactory;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private WhisperWorkerSession? _session;

    public WhisperWorkerSupervisor(
        WhisperWorkerSupervisorOptions? options = null,
        Func<IWhisperWorkerProcess>? processFactory = null)
    {
        _options = options ?? new WhisperWorkerSupervisorOptions();
        _processFactory = processFactory
            ?? (() => new WhisperWorkerProcess(
                _options.WorkerExecutablePath,
                _options.CaptureWorkerDiagnostics,
                _options.WorkerDiagnosticsDirectory));
    }

    public event Action<WhisperWorkerState>? StateChanged;

    public WhisperWorkerState State { get; private set; } = WhisperWorkerState.Idle;

    public WhisperWorkerSession? Session => _session;

    public async Task<WhisperWorkerSession> GetReadySessionAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var fingerprintHex = EngineSettingsFingerprint.Compute(settings);
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_session is { IsReady: true } current
                && string.Equals(current.FingerprintHex, fingerprintHex, StringComparison.Ordinal))
            {
                return current;
            }

            var replacement = await StartSessionAsync(settings, fingerprintHex, cancellationToken);
            var previous = _session;
            _session = replacement;
            if (previous is not null)
            {
                _ = Task.Run(() => previous.ShutdownAsync(CancellationToken.None), CancellationToken.None);
            }

            return replacement;
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task<WhisperWorkerSession> RestartSessionAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            var previous = _session;
            _session = null;
            if (previous is not null)
            {
                previous.Unavailable -= OnSessionUnavailable;
                previous.Dispose();
            }

            SetState(WhisperWorkerState.Recovering);
            var replacement = await StartSessionAsync(
                settings,
                EngineSettingsFingerprint.Compute(settings),
                cancellationToken);
            _session = replacement;
            SetState(WhisperWorkerState.Ready);
            return replacement;
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task<WhisperWorkerSession> StartSessionAsync(
        AppSettings settings,
        string fingerprintHex,
        CancellationToken cancellationToken)
    {
        SetState(WhisperWorkerState.Starting);
        var pipeName = $"LafazFlow.WhisperWorker.{Guid.NewGuid():N}";
        var process = _processFactory();
        var session = new WhisperWorkerSession(
            pipeName,
            process,
            fingerprintHex,
            _options.ReadinessTimeout,
            _options.OperationTimeout,
            _options.ShutdownTimeout);
        session.Unavailable += OnSessionUnavailable;

        try
        {
            SetState(WhisperWorkerState.Loading);
            await session.InitializeAsync(settings, cancellationToken);
            SetState(WhisperWorkerState.Ready);
            LogWorkerEvent($"state=ready pid={session.ProcessId} fingerprint={fingerprintHex[..12]}");
            return session;
        }
        catch (Exception error)
        {
            session.Unavailable -= OnSessionUnavailable;
            session.Dispose();
            SetState(WhisperWorkerState.Unavailable);
            LogWorkerEvent($"state=unavailable reason={ShortError(error)}");
            throw;
        }
    }

    private void OnSessionUnavailable(WhisperWorkerSession session)
    {
        if (!ReferenceEquals(session, _session))
        {
            return;
        }

        SetState(WhisperWorkerState.Recovering);
        LogWorkerEvent($"state=recovering pid={session.ProcessId}");
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        WhisperWorkerSession? session;
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            session = _session;
            _session = null;
        }
        finally
        {
            _startGate.Release();
        }

        if (session is not null)
        {
            await session.ShutdownAsync(cancellationToken);
        }

        SetState(WhisperWorkerState.Idle);
    }

    public void Dispose()
    {
        var session = _session;
        _session = null;
        session?.Dispose();
        _startGate.Dispose();
    }

    private void SetState(WhisperWorkerState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }

    private static string ShortError(Exception error)
    {
        return string.IsNullOrWhiteSpace(error.Message)
            ? error.GetType().Name
            : error.Message;
    }

    private static void LogWorkerEvent(string message)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LafazFlow",
                "Logs");
            BoundedLogFileWriter.AppendLine(
                Path.Combine(logRoot, "lafazflow.log"),
                $"[{DateTimeOffset.Now:O}] WORKER {message}");
        }
        catch
        {
        }
    }
}

public sealed class WhisperWorkerSession : IDisposable
{
    private readonly string _pipeName;
    private readonly IWhisperWorkerProcess _process;
    private readonly TimeSpan _readinessTimeout;
    private readonly TimeSpan _operationTimeout;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _responsesLock = new();
    private readonly Dictionary<Guid, TaskCompletionSource<WhisperPipeResponse>> _pendingResponses = [];
    private NamedPipeClientStream? _pipe;
    private Task? _readerTask;
    private bool _readerStarted;
    private bool _disposed;

    internal WhisperWorkerSession(
        string pipeName,
        IWhisperWorkerProcess process,
        string fingerprintHex,
        TimeSpan readinessTimeout,
        TimeSpan operationTimeout,
        TimeSpan shutdownTimeout)
    {
        _pipeName = pipeName;
        _process = process;
        FingerprintHex = fingerprintHex;
        _readinessTimeout = readinessTimeout;
        _operationTimeout = operationTimeout;
        _shutdownTimeout = shutdownTimeout;
        _process.Exited += OnProcessExited;
    }

    public event Action<WhisperWorkerSession>? Unavailable;

    public string FingerprintHex { get; }

    public int ProcessId => _process.Id;

    public bool IsReady { get; private set; }

    public async Task InitializeAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        _pipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        _process.Start(_pipeName, settings);

        using var readinessCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readinessCts.CancelAfter(_readinessTimeout);
        await _pipe.ConnectAsync(readinessCts.Token);
        EnsureReaderStarted();

        var request = new WhisperPipeRequest(
            WhisperPipeOp.Initialize,
            Guid.NewGuid(),
            _sessionId,
            (uint)_readinessTimeout.TotalMilliseconds,
            WhisperPipeProtocol.FingerprintHexToBytes(FingerprintHex),
            WhisperPipeProtocol.AudioFormatPcm16kMono,
            0,
            []);
        var response = await SendAndAwaitAsync(request, readinessCts.Token);
        if (response.Status != WhisperPipeStatus.Ok
            || !string.Equals(
                WhisperPipeProtocol.FingerprintBytesToHex(response.Fingerprint),
                FingerprintHex,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Worker initialization failed or fingerprint mismatched.");
        }

        IsReady = true;
    }

    public async Task<WhisperPipeResponse> TranscribeFinalAsync(
        byte[] pcmAudio,
        uint sampleCount,
        CancellationToken cancellationToken)
    {
        return await TranscribeAsync(WhisperPipeOp.Final, pcmAudio, sampleCount, cancellationToken);
    }

    public async Task<WhisperPipeResponse> TranscribePreviewAsync(
        byte[] pcmAudio,
        uint sampleCount,
        CancellationToken cancellationToken)
    {
        return await TranscribeAsync(WhisperPipeOp.Preview, pcmAudio, sampleCount, cancellationToken);
    }

    private async Task<WhisperPipeResponse> TranscribeAsync(
        WhisperPipeOp op,
        byte[] pcmAudio,
        uint sampleCount,
        CancellationToken cancellationToken)
    {
        EnsureUsable();
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCts.CancelAfter(_operationTimeout);
        var request = new WhisperPipeRequest(
            op,
            Guid.NewGuid(),
            _sessionId,
            (uint)_operationTimeout.TotalMilliseconds,
            WhisperPipeProtocol.FingerprintHexToBytes(FingerprintHex),
            WhisperPipeProtocol.AudioFormatPcm16kMono,
            sampleCount,
            pcmAudio);
        return await SendAndAwaitAsync(request, operationCts.Token);
    }

    public async Task CancelAsync(Guid requestId, CancellationToken cancellationToken)
    {
        EnsureUsable();
        var request = new WhisperPipeRequest(
            WhisperPipeOp.Cancel,
            Guid.NewGuid(),
            _sessionId,
            0,
            WhisperPipeProtocol.FingerprintHexToBytes(FingerprintHex),
            0,
            0,
            []);
        await WriteRequestAsync(request, cancellationToken);
    }

    public async Task<string> HealthAsync(CancellationToken cancellationToken)
    {
        EnsureUsable();
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCts.CancelAfter(_operationTimeout);
        var request = new WhisperPipeRequest(
            WhisperPipeOp.Health,
            Guid.NewGuid(),
            _sessionId,
            0,
            WhisperPipeProtocol.FingerprintHexToBytes(FingerprintHex),
            0,
            0,
            []);
        var response = await SendAndAwaitAsync(request, operationCts.Token);
        return Encoding.UTF8.GetString(response.Data);
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_pipe is { IsConnected: true })
            {
                var request = new WhisperPipeRequest(
                    WhisperPipeOp.Shutdown,
                    Guid.NewGuid(),
                    _sessionId,
                    0,
                    WhisperPipeProtocol.FingerprintHexToBytes(FingerprintHex),
                    0,
                    0,
                    []);
                _ = await SendAndAwaitAsync(request, cancellationToken);
            }
        }
        catch
        {
        }

        if (!_process.WaitForExit((int)_shutdownTimeout.TotalMilliseconds))
        {
            _process.KillExact();
            _process.WaitForExit((int)_shutdownTimeout.TotalMilliseconds);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.Exited -= OnProcessExited;
        _process.Dispose();
        _pipe?.Dispose();
        _pipe = null;
    }

    private void EnsureUsable()
    {
        if (_disposed || _pipe is null || !_pipe.IsConnected || _process.HasExited)
        {
            MarkUnavailable();
            throw new InvalidOperationException("Whisper worker is not connected.");
        }
    }

    private async Task WriteRequestAsync(WhisperPipeRequest request, CancellationToken cancellationToken)
    {
        var frame = WhisperPipeProtocol.EncodeRequest(request);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            if (_pipe is null)
            {
                throw new InvalidOperationException("Whisper worker pipe is not connected.");
            }

            await _pipe.WriteAsync(frame, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void EnsureReaderStarted()
    {
        if (_readerStarted)
        {
            return;
        }

        _readerStarted = true;
        _readerTask = Task.Run(ResponseReaderLoopAsync, CancellationToken.None);
    }

    private async Task ResponseReaderLoopAsync()
    {
        try
        {
            while (true)
            {
                var payload = await ReadFramePayloadAsync(_pipe!, CancellationToken.None);
                if (!WhisperPipeProtocol.TryDecodeResponse(payload, out var response, out _))
                {
                    break;
                }

                TaskCompletionSource<WhisperPipeResponse>? tcs;
                lock (_responsesLock)
                {
                    _pendingResponses.Remove(response.RequestId, out tcs);
                }

                tcs?.TrySetResult(response);
            }
        }
        catch
        {
        }

        MarkUnavailable();
    }

    private async Task<WhisperPipeResponse> SendAndAwaitAsync(
        WhisperPipeRequest request,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<WhisperPipeResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_responsesLock)
        {
            _pendingResponses[request.RequestId] = tcs;
        }

        try
        {
            await WriteRequestAsync(request, cancellationToken);
            return await tcs.Task.WaitAsync(cancellationToken);
        }
        catch
        {
            lock (_responsesLock)
            {
                _pendingResponses.Remove(request.RequestId);
            }

            throw;
        }
    }

    private static async Task<byte[]> ReadFramePayloadAsync(
        NamedPipeClientStream pipe,
        CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];
        await ReadExactAsync(pipe, lengthBytes, cancellationToken);
        var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        if (length < WhisperPipeProtocol.HeaderBytes || length > WhisperPipeProtocol.MaxFrameBytes)
        {
            throw new InvalidOperationException("Invalid frame length from Whisper worker.");
        }

        var payload = new byte[length];
        await ReadExactAsync(pipe, payload, cancellationToken);
        return payload;
    }

    private static async Task ReadExactAsync(
        NamedPipeClientStream pipe,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await pipe.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Whisper worker closed the pipe.");
            }

            offset += read;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        MarkUnavailable();
    }

    private void MarkUnavailable()
    {
        IsReady = false;
        Unavailable?.Invoke(this);
    }

}
