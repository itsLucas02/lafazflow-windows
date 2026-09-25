using LafazFlow.Windows.Services;
using NAudio.Wave;

namespace LafazFlow.Windows.Tests;

public sealed class NativePcm16ResamplerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ConvertsNativeFloatMicAudioToContinuous16kMonoPcm(int channels)
    {
        var converter = new NativePcm16Resampler(WaveFormat.CreateIeeeFloatWaveFormat(48000, channels));
        var chunks = new List<byte[]>();
        for (var part = 0; part < 100; part++)
        {
            var native = new byte[480 * channels * sizeof(float)];
            for (var frame = 0; frame < 480; frame++)
            {
                var sample = (float)(0.3 * Math.Sin(2 * Math.PI * 440 * (part * 480 + frame) / 48000));
                for (var channel = 0; channel < channels; channel++)
                    BitConverter.TryWriteBytes(native.AsSpan((frame * channels + channel) * sizeof(float), sizeof(float)), sample);
            }
            converter.Add(native, native.Length, chunks.Add);
        }
        converter.Flush(chunks.Add);

        var pcm = chunks.SelectMany(chunk => chunk).ToArray();
        Assert.InRange(pcm.Length / 2, 16700, 16900); // One second of input plus the 50 ms stop drain.
        Assert.All(chunks, chunk => Assert.Equal(0, chunk.Length % 2));
        var max = Enumerable.Range(1600, 16000 - 3200)
            .Max(index => Math.Abs((int)BitConverter.ToInt16(pcm, index * 2)));
        Assert.InRange(max, 9000, 10500);
    }
}
