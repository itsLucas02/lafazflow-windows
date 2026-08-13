using System.IO.Pipes;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperWorkerSupervisorTests
{
    [Fact]
    public async Task TwoConcurrentStartupCallsProduceOneWorker()
    {
        var started = 0;
        var factory = new FakeWorkerProcessFactory(() => Interlocked.Increment(ref started));
        using var supervisor = CreateSupervisor(factory);
        var settings = AppSettings.Default;

        var first = supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        var second = supervisor.GetReadySessionAsync(settings, CancellationToken.None);
        var sessions = await Task.WhenAll(first, second);

        Assert.Same(sessions[0], sessions[1]);
        Assert.Equal(1, started);
        Assert.True(sessions[0].IsReady);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task FingerprintChangeStartsReplacementWorker()
    {
        var started = 0;
        var factory = new FakeWorkerProcessFactory(() => Interlocked.Increment(ref started));
        using var supervisor = CreateSupervisor(factory);

        var first = await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        var second = await supervisor.GetReadySessionAsync(
            AppSettings.Default with { WhisperThreads = 8 },
            CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, started);
        Assert.NotEqual(first.FingerprintHex, second.FingerprintHex);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task WorkerExitMovesStateToRecovering()
    {
        var factory = new FakeWorkerProcessFactory(() => { });
        using var supervisor = CreateSupervisor(factory);
        var states = new List<WhisperWorkerState>();
        supervisor.StateChanged += states.Add;

        var session = await supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None);
        factory.LatestProcess?.Disconnect();
        await WaitUntilAsync(() => supervisor.State == WhisperWorkerState.Recovering);

        Assert.Contains(WhisperWorkerState.Recovering, states);
        await supervisor.ShutdownAsync();
    }

    [Fact]
    public async Task ReadinessTimeoutMovesStateToUnavailable()
    {
        var factory = new FakeWorkerProcessFactory(() => { }, respondToInitialize: false);
        var options = new WhisperWorkerSupervisorOptions
        {
            ReadinessTimeout = TimeSpan.FromMilliseconds(200),
            OperationTimeout = TimeSpan.FromSeconds(5)
        };
        using var supervisor = CreateSupervisor(factory, options);
        var states = new List<WhisperWorkerState>();
        supervisor.StateChanged += states.Add;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.GetReadySessionAsync(AppSettings.Default, CancellationToken.None));

        Assert.Contains(WhisperWorkerState.Unavailable, states);
    }

    private static WhisperWorkerSupervisor CreateSupervisor(
        FakeWorkerProcessFactory factory,
        WhisperWorkerSupervisorOptions? options = null)
    {
        return new WhisperWorkerSupervisor(
            options ?? new WhisperWorkerSupervisorOptions
            {
                ReadinessTimeout = TimeSpan.FromSeconds(10),
                OperationTimeout = TimeSpan.FromSeconds(10),
                ShutdownTimeout = TimeSpan.FromSeconds(2)
            },
            () => factory.Create());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    private sealed class FakeWorkerProcessFactory
    {
        private readonly Action _onStart;
        private readonly bool _respondToInitialize;

        public FakeWorkerProcessFactory(Action onStart, bool respondToInitialize = true)
        {
            _onStart = onStart;
            _respondToInitialize = respondToInitialize;
        }

        public FakeWorkerProcess? LatestProcess { get; private set; }

        public FakeWorkerProcess Create()
        {
            LatestProcess = new FakeWorkerProcess(_onStart, _respondToInitialize);
            return LatestProcess;
        }
    }

    private sealed class FakeWorkerProcess : IWhisperWorkerProcess
    {
        private readonly Action _onStart;
        private readonly bool _respondToInitialize;
        private NamedPipeServerStream? _server;
        private Task? _serveTask;
        private bool _disconnected;

        public FakeWorkerProcess(Action onStart, bool respondToInitialize)
        {
            _onStart = onStart;
            _respondToInitialize = respondToInitialize;
        }

        public int Id { get; private set; } = Random.Shared.Next(1000, 9999);

        public bool HasExited => _disconnected;

        public event EventHandler? Exited;

        public void Start(string pipeName, AppSettings settings)
        {
            _onStart();
            _serveTask = Task.Run(() => ServeAsync(pipeName, settings));
        }

        public void KillExact()
        {
            Disconnect();
        }

        public bool WaitForExit(int timeoutMilliseconds)
        {
            return true;
        }

        public void Dispose()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            if (_disconnected)
            {
                return;
            }

            _disconnected = true;
            _server?.Dispose();
            _server = null;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        private async Task ServeAsync(string pipeName, AppSettings settings)
        {
            var fingerprintHex = EngineSettingsFingerprint.Compute(settings);
            var fingerprint = WhisperPipeProtocol.FingerprintHexToBytes(fingerprintHex);
            _server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await _server.WaitForConnectionAsync();

            while (!_disconnected)
            {
                var payload = await ReadFrameAsync();
                if (payload is null)
                {
                    break;
                }

                if (!WhisperPipeProtocol.TryDecodeRequest(payload, out var request, out _))
                {
                    break;
                }

                if (request.Op == WhisperPipeOp.Initialize)
                {
                    if (_respondToInitialize)
                    {
                        await WriteResponseAsync(new WhisperPipeResponse(
                            WhisperPipeOp.Initialize,
                            WhisperPipeStatus.Ok,
                            request.RequestId,
                            request.SessionId,
                            fingerprint,
                            new byte[8]));
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
                else if (request.Op == WhisperPipeOp.Health)
                {
                    await WriteResponseAsync(new WhisperPipeResponse(
                        WhisperPipeOp.Health,
                        WhisperPipeStatus.Ok,
                        request.RequestId,
                        request.SessionId,
                        fingerprint,
                        "uptime_ms=1 completed=1 last_failure=none model=fake backend=cuda fingerprint="u8.ToArray()));
                }
                else if (request.Op == WhisperPipeOp.Shutdown)
                {
                    await WriteResponseAsync(new WhisperPipeResponse(
                        WhisperPipeOp.Shutdown,
                        WhisperPipeStatus.Ok,
                        request.RequestId,
                        request.SessionId,
                        fingerprint,
                        []));
                    Disconnect();
                    break;
                }
                else if (request.Op == WhisperPipeOp.Final || request.Op == WhisperPipeOp.Preview)
                {
                    await WriteResponseAsync(new WhisperPipeResponse(
                        request.Op,
                        WhisperPipeStatus.Ok,
                        request.RequestId,
                        request.SessionId,
                        fingerprint,
                        "fake transcript"u8.ToArray()));
                }
            }
        }

        private async Task<byte[]?> ReadFrameAsync()
        {
            var lengthBytes = new byte[4];
            try
            {
                await ReadExactAsync(lengthBytes);
            }
            catch
            {
                return null;
            }

            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
            var payload = new byte[length];
            await ReadExactAsync(payload);
            return payload;
        }

        private async Task ReadExactAsync(byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await _server!.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset));
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
            }
        }

        private async Task WriteResponseAsync(WhisperPipeResponse response)
        {
            var frame = WhisperPipeProtocol.EncodeResponse(response);
            await _server!.WriteAsync(frame);
            await _server.FlushAsync();
        }
    }
}
