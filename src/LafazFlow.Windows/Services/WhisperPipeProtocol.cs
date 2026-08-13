using System.Buffers.Binary;

namespace LafazFlow.Windows.Services;

public enum WhisperPipeOp : byte
{
    Initialize = 1,
    Preview = 2,
    Final = 3,
    Cancel = 4,
    Health = 5,
    Shutdown = 6
}

public enum WhisperPipeStatus : byte
{
    Ok = 0,
    Aborted = 1,
    InvalidRequest = 2,
    Busy = 3,
    InternalError = 4,
    Timeout = 5,
    Unavailable = 6
}

public static class WhisperPipeProtocol
{
    public const byte Version = 1;
    public const uint MaxFrameBytes = 16u * 1024u * 1024u;
    public const int HeaderBytes = 80;
    public const int FingerprintBytes = 32;
    public const uint AudioFormatPcm16kMono = 1;

    public static bool IsRequestOp(WhisperPipeOp op)
    {
        return op is >= WhisperPipeOp.Initialize and <= WhisperPipeOp.Shutdown;
    }

    public static byte[] EncodeRequest(WhisperPipeRequest request)
    {
        if (request.Fingerprint.Length != FingerprintBytes)
        {
            throw new ArgumentException("Fingerprint must be exactly 32 bytes.", nameof(request));
        }

        var payload = new byte[HeaderBytes + request.Data.Length];
        payload[0] = Version;
        payload[1] = (byte)request.Op;
        payload[2] = 0;
        payload[3] = 0;
        request.RequestId.TryWriteBytes(payload.AsSpan(4, 16));
        request.SessionId.TryWriteBytes(payload.AsSpan(20, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(36, 4), request.DeadlineMs);
        request.Fingerprint.CopyTo(payload, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(72, 4), request.AudioFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(76, 4), request.SampleCount);
        request.Data.CopyTo(payload, HeaderBytes);
        return Frame(payload);
    }

    public static byte[] EncodeResponse(WhisperPipeResponse response)
    {
        var payload = new byte[HeaderBytes + response.Data.Length];
        payload[0] = Version;
        payload[1] = (byte)(0x80 | (byte)response.Op);
        payload[2] = (byte)response.Status;
        payload[3] = 0;
        response.RequestId.TryWriteBytes(payload.AsSpan(4, 16));
        response.SessionId.TryWriteBytes(payload.AsSpan(20, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(36, 4), 0);
        response.Fingerprint.CopyTo(payload, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(72, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(76, 4), 0);
        response.Data.CopyTo(payload, HeaderBytes);
        return Frame(payload);
    }

    public static bool TryDecodeRequest(ReadOnlySpan<byte> payload, out WhisperPipeRequest request, out string? error)
    {
        request = null!;
        error = null;
        if (payload.Length < HeaderBytes)
        {
            error = "frame too short";
            return false;
        }

        if (payload[0] != Version)
        {
            error = "protocol version mismatch";
            return false;
        }

        var op = (WhisperPipeOp)payload[1];
        if (!IsRequestOp(op))
        {
            error = "unknown operation";
            return false;
        }

        var fingerprint = payload.Slice(40, FingerprintBytes).ToArray();
        var audioFormat = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(72, 4));
        var sampleCount = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(76, 4));
        request = new WhisperPipeRequest(
            op,
            new Guid(payload.Slice(4, 16)),
            new Guid(payload.Slice(20, 16)),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(36, 4)),
            fingerprint,
            audioFormat,
            sampleCount,
            payload[HeaderBytes..].ToArray());
        return true;
    }

    public static bool TryDecodeResponse(ReadOnlySpan<byte> payload, out WhisperPipeResponse response, out string? error)
    {
        response = null!;
        error = null;
        if (payload.Length < HeaderBytes)
        {
            error = "frame too short";
            return false;
        }

        if (payload[0] != Version)
        {
            error = "protocol version mismatch";
            return false;
        }

        var kind = payload[1];
        if ((kind & 0x80) == 0)
        {
            error = "response frame has request kind";
            return false;
        }

        var op = (WhisperPipeOp)(kind & 0x7F);
        var status = (WhisperPipeStatus)payload[2];
        response = new WhisperPipeResponse(
            op,
            status,
            new Guid(payload.Slice(4, 16)),
            new Guid(payload.Slice(20, 16)),
            payload.Slice(40, FingerprintBytes).ToArray(),
            payload[HeaderBytes..].ToArray());
        return true;
    }

    public static bool TryDecodeFrame(ReadOnlySpan<byte> frame, out byte[] payload, out string? error)
    {
        payload = [];
        error = null;
        if (frame.Length < 4)
        {
            error = "missing length prefix";
            return false;
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        if (length < HeaderBytes || length > MaxFrameBytes)
        {
            error = "invalid frame length";
            return false;
        }

        if (frame.Length != length + 4)
        {
            error = "frame length does not match payload";
            return false;
        }

        payload = frame[4..].ToArray();
        return true;
    }

    private static byte[] Frame(byte[] payload)
    {
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame, 4);
        return frame;
    }

    public static string FingerprintBytesToHex(byte[] fingerprint)
    {
        return Convert.ToHexString(fingerprint);
    }

    public static byte[] FingerprintHexToBytes(string hex)
    {
        if (hex.Length != FingerprintBytes * 2)
        {
            throw new ArgumentException("Fingerprint hex must be 64 characters.", nameof(hex));
        }

        return Convert.FromHexString(hex);
    }
}

public sealed record WhisperPipeRequest(
    WhisperPipeOp Op,
    Guid RequestId,
    Guid SessionId,
    uint DeadlineMs,
    byte[] Fingerprint,
    uint AudioFormat,
    uint SampleCount,
    byte[] Data);

public sealed record WhisperPipeResponse(
    WhisperPipeOp Op,
    WhisperPipeStatus Status,
    Guid RequestId,
    Guid SessionId,
    byte[] Fingerprint,
    byte[] Data);
