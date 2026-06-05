using System.IO;

namespace LafazFlow.Windows.Services;

public sealed record AudioSignalAnalysis(
    bool IsUsable,
    double PeakLevel,
    double RmsLevel,
    double DurationSeconds)
{
    private const double SilentPeakThreshold = 0.003;
    private const double SilentRmsThreshold = 0.0008;

    public bool IsEffectivelySilent =>
        IsUsable
        && PeakLevel < SilentPeakThreshold
        && RmsLevel < SilentRmsThreshold;

    public static AudioSignalAnalysis Unknown { get; } = new(false, 0, 0, 0);
}

public static class AudioSignalAnalyzer
{
    public static AudioSignalAnalysis AnalyzeWav(string path)
    {
        if (!File.Exists(path))
        {
            return AudioSignalAnalysis.Unknown;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (ReadAscii(reader, 4) != "RIFF")
            {
                return AudioSignalAnalysis.Unknown;
            }

            _ = reader.ReadInt32();
            if (ReadAscii(reader, 4) != "WAVE")
            {
                return AudioSignalAnalysis.Unknown;
            }

            short channels = 0;
            int sampleRate = 0;
            short bitsPerSample = 0;
            long dataPosition = -1;
            int dataLength = 0;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = ReadAscii(reader, 4);
                var chunkSize = reader.ReadInt32();
                var nextChunk = stream.Position + chunkSize;

                if (chunkId == "fmt " && chunkSize >= 16)
                {
                    var audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    if (audioFormat != 1 || bitsPerSample != 16 || channels <= 0 || sampleRate <= 0)
                    {
                        return AudioSignalAnalysis.Unknown;
                    }
                }
                else if (chunkId == "data")
                {
                    dataPosition = stream.Position;
                    dataLength = chunkSize;
                }

                stream.Position = Math.Min(nextChunk + (chunkSize % 2), stream.Length);
            }

            if (dataPosition < 0 || dataLength < 2 || bitsPerSample != 16 || channels <= 0 || sampleRate <= 0)
            {
                return AudioSignalAnalysis.Unknown;
            }

            stream.Position = dataPosition;
            var sampleCount = dataLength / 2;
            var sumSquares = 0.0;
            var peak = 0.0;

            for (var index = 0; index < sampleCount; index++)
            {
                var sample = reader.ReadInt16() / 32768.0;
                var abs = Math.Abs(sample);
                peak = Math.Max(peak, abs);
                sumSquares += sample * sample;
            }

            var rms = Math.Sqrt(sumSquares / Math.Max(1, sampleCount));
            var duration = sampleCount / (double)(sampleRate * channels);

            return new AudioSignalAnalysis(true, peak, rms, duration);
        }
        catch
        {
            return AudioSignalAnalysis.Unknown;
        }
    }

    private static string ReadAscii(BinaryReader reader, int count)
    {
        return System.Text.Encoding.ASCII.GetString(reader.ReadBytes(count));
    }
}
