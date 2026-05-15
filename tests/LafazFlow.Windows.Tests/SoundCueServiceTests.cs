using LafazFlow.Windows.Services;

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
        Assert.Equal(0.4f, player.Volume);
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
    public void BundledSoundCueFilesAreCopiedToOutput()
    {
        var soundRoot = Path.Combine(AppContext.BaseDirectory, "Resources", "Sounds");

        Assert.True(File.Exists(Path.Combine(soundRoot, "recstart.mp3")));
        Assert.True(File.Exists(Path.Combine(soundRoot, "recstop.mp3")));
        Assert.True(File.Exists(Path.Combine(soundRoot, "pastess.mp3")));
        Assert.True(File.Exists(Path.Combine(soundRoot, "esc.wav")));
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
}
