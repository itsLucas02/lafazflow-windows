using LafazFlow.Windows.Services;
using NAudio.Wave;

namespace LafazFlow.Windows.Tests;

public sealed class CaptureOnsetMetricsTests
{
    [Fact]
    public void ReportsSamplePositionBoundaryJumpAndEarlySignalWithoutLoggingSpeech()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lafazflow-metrics-{Guid.NewGuid():N}.wav");
        try
        {
            var samples = new short[16000];
            samples[7999] = -100;
            samples[8000] = 100;
            Array.Fill(samples, (short)16384, 8100, 8000 - 100);
            using (var writer = new WaveFileWriter(path, new WaveFormat(16000, 16, 1)))
            {
                var bytes = new byte[samples.Length * 2];
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                writer.Write(bytes);
            }

            var metrics = CaptureOnsetMetrics.Analyze(path, preRollBytes: 16000);

            Assert.Equal(7999, metrics.FirstAbovePointOnePercentSample);
            Assert.Equal(8100, metrics.FirstAboveOnePercentSample);
            Assert.Equal(200, metrics.BoundaryJumpPcm);
            Assert.Equal(0.5, metrics.RmsFirst100, precision: 4);
            Assert.Equal(0.5, metrics.PeakFirst500, precision: 4);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
