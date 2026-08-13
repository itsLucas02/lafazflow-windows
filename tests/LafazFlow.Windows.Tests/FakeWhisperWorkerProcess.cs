using System.Buffers.Binary;
using System.IO.Pipes;
using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

internal sealed class FakeWorkerProcessFactory
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

internal sealed class FakeWorkerProcess : IWhisperWorkerProcess
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

        var length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
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
