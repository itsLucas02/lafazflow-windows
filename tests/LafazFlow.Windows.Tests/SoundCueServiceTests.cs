using LafazFlow.Windows.Services;
using NAudio.Wave;

namespace LafazFlow.Windows.Tests;

public sealed class SoundCueServiceTests
{
    [Theory]
    [InlineData(SoundCueKind.RecordingStarted, "recstart.mp3")]
    [InlineData(SoundCueKind.TranscribingStarted, "recstop.mp3")]
    [InlineData(SoundCueKind.Completed, "pastess.mp3")]
    [InlineData(SoundCueKind.Error, "esc.wav")]
    public void GetFileNameMapsCueKindsToBundledAssets(SoundCueKind kind, string expectedFileName)
    {
        Assert.Equal(expectedFileName, SoundCueService.GetFileName(kind));
    }

    [Fact]
    public void PlayUsesResolvedAssetPathWhenFileExists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var assetPath = Path.Combine(root, "recstart.mp3");
        File.WriteAllBytes(assetPath, [1, 2, 3]);
        var player = new RecordingSoundCuePlayer();
        var service = new SoundCueService(root, player);

        service.PlayRecordingStarted();

        Assert.Equal(assetPath, player.PlayedPath);
        Assert.Equal(0.5f, player.Volume);
    }

    [Fact]
    public void PlayUsesConfiguredVolumeWhenFileExists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var assetPath = Path.Combine(root, "pastess.mp3");
        File.WriteAllBytes(assetPath, [1, 2, 3]);
        var player = new RecordingSoundCuePlayer();
        var service = new SoundCueService(root, player);

        service.PlayCompleted(new SoundCueOptions(true, 0.75f));

        Assert.Equal(assetPath, player.PlayedPath);
        Assert.Equal(0.75f, player.Volume);
    }

    [Theory]
    [InlineData(SoundCueKind.RecordingStarted, 1.0f)]
    [InlineData(SoundCueKind.TranscribingStarted, 1.0f)]
    [InlineData(SoundCueKind.Completed, 1.0f)]
    [InlineData(SoundCueKind.Error, 1.0f)]
    public void GetCueGainKeepsSettingsVolumeAudible(SoundCueKind kind, float expectedGain)
    {
        Assert.Equal(expectedGain, SoundCueService.GetCueGain(kind));
    }

    [Theory]
    [InlineData(SoundCueKind.RecordingStarted, 0.5f, 0.5f)]
    [InlineData(SoundCueKind.TranscribingStarted, 0.5f, 0.5f)]
    [InlineData(SoundCueKind.Completed, 0.5f, 0.5f)]
    [InlineData(SoundCueKind.Error, 0.5f, 0.5f)]
    [InlineData(SoundCueKind.TranscribingStarted, 2.0f, 1.0f)]
    public void ResolvePlaybackVolumeAppliesGlobalVolumeAndCueGain(
        SoundCueKind kind,
        float inputVolume,
        float expectedVolume)
    {
        var volume = SoundCueService.ResolvePlaybackVolume(kind, new SoundCueOptions(true, inputVolume));

        Assert.Equal(expectedVolume, volume, precision: 6);
    }

    [Fact]
    public void PlaySkipsWhenCuesAreDisabled()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "recstart.mp3"), [1, 2, 3]);
        var player = new RecordingSoundCuePlayer();
        var service = new SoundCueService(root, player);

        service.PlayRecordingStarted(new SoundCueOptions(false, 0.5f));

        Assert.Null(player.PlayedPath);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(1.4f, 1f)]
    public void PlayClampsConfiguredVolume(float inputVolume, float expectedVolume)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "recstart.mp3"), [1, 2, 3]);
        var player = new RecordingSoundCuePlayer();
        var service = new SoundCueService(root, player);

        service.PlayRecordingStarted(new SoundCueOptions(true, inputVolume));

        Assert.Equal(expectedVolume, player.Volume);
    }

    [Fact]
    public void PlaySkipsMissingAssets()
    {
        var player = new RecordingSoundCuePlayer();
        var service = new SoundCueService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), player);

        service.PlayCompleted();

        Assert.Null(player.PlayedPath);
    }

    [Fact]
    public void PlayIgnoresPlayerExceptions()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "esc.wav"), [1, 2, 3]);
        var service = new SoundCueService(root, new ThrowingSoundCuePlayer());

        var exception = Record.Exception(() => service.PlayError());

        Assert.Null(exception);
    }

    [Fact]
    public void BundledSoundCueFilesAreCopiedToOutput()
    {
        var soundRoot = Path.Combine(AppContext.BaseDirectory, "Resources", "Sounds");

        Assert.True(File.Exists(Path.Combine(soundRoot, "recstart.mp3")));
        Assert.True(File.Exists(Path.Combine(soundRoot, "recstop.mp3")));
        Assert.True(File.Exists(Path.Combine(soundRoot, "pastess.mp3")));
        Assert.True(File.Exists(Path.Combine(soundRoot, "esc.wav")));
    }

    [Theory]
    [InlineData("recstart.mp3", 0.55)]
    [InlineData("recstop.mp3", 0.55)]
    public void StartAndStopCuesStayBriefForResponsiveFeedback(string fileName, double maxDurationSeconds)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Sounds", fileName);

        using var reader = new AudioFileReader(path);

        Assert.True(
            reader.TotalTime.TotalSeconds <= maxDurationSeconds,
            $"{fileName} is {reader.TotalTime.TotalSeconds:0.000}s; expected <= {maxDurationSeconds:0.000}s.");
    }

    private sealed class RecordingSoundCuePlayer : ISoundCuePlayer
    {
        public string? PlayedPath { get; private set; }

        public float Volume { get; private set; }

        public void Play(string path, float volume)
        {
            PlayedPath = path;
            Volume = volume;
        }
    }

    private sealed class ThrowingSoundCuePlayer : ISoundCuePlayer
    {
        public void Play(string path, float volume)
        {
            throw new InvalidOperationException("Audio output unavailable.");
        }
    }
}
