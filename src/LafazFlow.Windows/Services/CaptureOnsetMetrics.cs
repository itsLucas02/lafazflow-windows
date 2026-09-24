using NAudio.Wave;

namespace LafazFlow.Windows.Services;

internal readonly record struct CaptureOnsetMetrics(
    long FirstAbovePointOnePercentSample,
    long FirstAboveOnePercentSample,
    int BoundaryJumpPcm,
    double RmsFirst100,
    double PeakFirst100,
    double RmsFirst500,
    double PeakFirst500)
{
    public static CaptureOnsetMetrics Analyze(string path, int preRollBytes)
    {
        try
        {
            using var reader = new WaveFileReader(path);
            if (reader.WaveFormat is not { SampleRate: 16000, Channels: 1, BitsPerSample: 16 })
            {
                return new(-1, -1, -1, 0, 0, 0, 0);
            }

            var bytes = new byte[(int)Math.Min(reader.Length, 16000L * 2 * 10)];
            var read = 0;
            while (read < bytes.Length)
            {
                var count = reader.Read(bytes, read, bytes.Length - read);
                if (count == 0) break;
                read += count;
            }

            var samples = read / 2;
            var boundarySample = preRollBytes / 2;
            var boundaryJump = -1;
            var firstQuiet = -1L;
            var first = -1L;
            var sum100 = 0.0;
            var sum500 = 0.0;
            var peak100 = 0.0;
            var peak500 = 0.0;
            var count100 = 0;
            var count500 = 0;
            short previous = 0;
            for (var index = 0; index < samples; index++)
            {
                var sample = BitConverter.ToInt16(bytes, index * 2);
                if (index == boundarySample && index > 0)
                {
                    boundaryJump = Math.Abs(sample - previous);
                }

                previous = sample;
                if (firstQuiet < 0 && Math.Abs((int)sample) >= 33) firstQuiet = index;
                if (first < 0 && Math.Abs((int)sample) >= 328) first = index;
                if (first < 0 || index - first >= 8000) continue;

                var level = sample / 32768.0;
                var absolute = Math.Abs(level);
                sum500 += level * level;
                peak500 = Math.Max(peak500, absolute);
                count500++;
                if (index - first < 1600)
                {
                    sum100 += level * level;
                    peak100 = Math.Max(peak100, absolute);
                    count100++;
                }
            }

            return new(
                firstQuiet,
                first,
                boundaryJump,
                Math.Sqrt(sum100 / Math.Max(1, count100)),
                peak100,
                Math.Sqrt(sum500 / Math.Max(1, count500)),
                peak500);
        }
        catch
        {
            return new(-1, -1, -1, 0, 0, 0, 0);
        }
    }
}
