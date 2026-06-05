using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class AudioSignalAnalyzerTests
{
    [Fact]
    public void AnalyzeWavMarksNearSilentRecordingAsSilent()
    {
        var path = WritePcm16Wav(Enumerable.Repeat((short)2, 16000));

        var analysis = AudioSignalAnalyzer.AnalyzeWav(path);

        Assert.True(analysis.IsUsable);
        Assert.True(analysis.IsEffectivelySilent);
        Assert.True(analysis.PeakLevel < 0.001);
        Assert.True(analysis.RmsLevel < 0.001);
    }

    [Fact]
    public void AnalyzeWavMarksNormalSpeechLevelAsAudible()
    {
        var samples = Enumerable.Range(0, 16000)
            .Select(index => (short)(Math.Sin(index * 0.08) * 3000));
        var path = WritePcm16Wav(samples);

        var analysis = AudioSignalAnalyzer.AnalyzeWav(path);

        Assert.True(analysis.IsUsable);
        Assert.False(analysis.IsEffectivelySilent);
        Assert.True(analysis.PeakLevel > 0.05);
    }

    private static string WritePcm16Wav(IEnumerable<short> samples)
    {
        var sampleArray = samples.ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var dataLength = sampleArray.Length * sizeof(short);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(16000 * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataLength);

        foreach (var sample in sampleArray)
        {
            writer.Write(sample);
        }

        return path;
    }
}
