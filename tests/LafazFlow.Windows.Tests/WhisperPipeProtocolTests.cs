using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperPipeProtocolTests
{
    [Fact]
    public void RequestRoundTripsThroughCodec()
    {
        var request = new WhisperPipeRequest(
            WhisperPipeOp.Final,
            Guid.NewGuid(),
            Guid.NewGuid(),
            5000,
            Enumerable.Repeat((byte)7, 32).ToArray(),
            WhisperPipeProtocol.AudioFormatPcm16kMono,
            1600,
            [1, 2, 3, 4, 5]);

        var frame = WhisperPipeProtocol.EncodeRequest(request);
        Assert.True(WhisperPipeProtocol.TryDecodeFrame(frame, out var payload, out var frameError));
        Assert.Null(frameError);
        Assert.True(WhisperPipeProtocol.TryDecodeRequest(payload, out var decoded, out var error));

        Assert.Null(error);
        Assert.Equal(request.Op, decoded.Op);
        Assert.Equal(request.RequestId, decoded.RequestId);
        Assert.Equal(request.SessionId, decoded.SessionId);
        Assert.Equal(request.DeadlineMs, decoded.DeadlineMs);
        Assert.Equal(request.Fingerprint, decoded.Fingerprint);
        Assert.Equal(request.AudioFormat, decoded.AudioFormat);
        Assert.Equal(request.SampleCount, decoded.SampleCount);
        Assert.Equal(request.Data, decoded.Data);
    }

    [Fact]
    public void ResponseRoundTripsThroughCodec()
    {
        var response = new WhisperPipeResponse(
            WhisperPipeOp.Final,
            WhisperPipeStatus.Ok,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Enumerable.Repeat((byte)3, 32).ToArray(),
            "hello world"u8.ToArray());

        var frame = WhisperPipeProtocol.EncodeResponse(response);
        Assert.True(WhisperPipeProtocol.TryDecodeFrame(frame, out var payload, out var frameError));
        Assert.Null(frameError);
        Assert.True(WhisperPipeProtocol.TryDecodeResponse(payload, out var decoded, out var error));

        Assert.Null(error);
        Assert.Equal(response.Op, decoded.Op);
        Assert.Equal(response.Status, decoded.Status);
        Assert.Equal(response.RequestId, decoded.RequestId);
        Assert.Equal(response.SessionId, decoded.SessionId);
        Assert.Equal(response.Fingerprint, decoded.Fingerprint);
        Assert.Equal(response.Data, decoded.Data);
    }

    [Fact]
    public void FrameValidationRejectsMalformedAndOversizedFrames()
    {
        Assert.False(WhisperPipeProtocol.TryDecodeFrame([1, 2, 3], out _, out var shortError));
        Assert.NotNull(shortError);

        var oversized = new byte[WhisperPipeProtocol.MaxFrameBytes + 8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(oversized, WhisperPipeProtocol.MaxFrameBytes + 1);
        Assert.False(WhisperPipeProtocol.TryDecodeFrame(oversized, out _, out var sizeError));
        Assert.NotNull(sizeError);

        var mismatch = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(mismatch, 100);
        Assert.False(WhisperPipeProtocol.TryDecodeFrame(mismatch, out _, out var lengthError));
        Assert.NotNull(lengthError);
    }

    [Fact]
    public void DecodeRejectsInvalidVersionAndUnknownOperations()
    {
        var payload = new byte[WhisperPipeProtocol.HeaderBytes];
        payload[0] = 99;
        Assert.False(WhisperPipeProtocol.TryDecodeRequest(payload, out _, out var versionError));
        Assert.Contains("version", versionError, StringComparison.OrdinalIgnoreCase);

        payload[0] = WhisperPipeProtocol.Version;
        payload[1] = 200;
        Assert.False(WhisperPipeProtocol.TryDecodeRequest(payload, out _, out var opError));
        Assert.Contains("operation", opError, StringComparison.OrdinalIgnoreCase);

        payload[1] = 0x80 | (byte)WhisperPipeOp.Final;
        Assert.False(WhisperPipeProtocol.TryDecodeRequest(payload, out _, out var kindError));
        Assert.NotNull(kindError);
    }

    [Fact]
    public void EncodeRejectsWrongFingerprintLength()
    {
        var request = new WhisperPipeRequest(
            WhisperPipeOp.Health,
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            [1, 2, 3],
            0,
            0,
            []);

        var error = Assert.Throws<ArgumentException>(() => WhisperPipeProtocol.EncodeRequest(request));
        Assert.Contains("32", error.Message);
    }

    [Fact]
    public void FingerprintHexConversionsRoundTrip()
    {
        var bytes = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();

        Assert.Equal(bytes, WhisperPipeProtocol.FingerprintHexToBytes(WhisperPipeProtocol.FingerprintBytesToHex(bytes)));
    }
}
