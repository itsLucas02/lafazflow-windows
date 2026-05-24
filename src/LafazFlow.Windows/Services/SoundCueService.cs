using System.IO;
using LafazFlow.Windows.Core;
using NAudio.Wave;

namespace LafazFlow.Windows.Services;

public enum SoundCueKind
{
    RecordingStarted,
    TranscribingStarted,
    Completed,
    Error
}

public interface ISoundCuePlayer
{
    void Play(string path, float volume);
}

public readonly record struct SoundCueOptions(bool Enabled, float Volume)
{
    public static SoundCueOptions Default { get; } = new(true, 0.5f);

    public static SoundCueOptions FromSettings(AppSettings settings)
    {
        return new SoundCueOptions(
            settings.EnableSoundCues,
            (float)Math.Clamp(settings.SoundCueVolume, 0, 1));
    }
}

public sealed class SoundCueService
{
    private readonly string _soundRoot;
    private readonly ISoundCuePlayer _player;

    public SoundCueService()
        : this(Path.Combine(AppContext.BaseDirectory, "Resources", "Sounds"), new NAudioSoundCuePlayer())
    {
    }

    public SoundCueService(string soundRoot, ISoundCuePlayer player)
    {
        _soundRoot = soundRoot;
        _player = player;
    }

    public void PlayRecordingStarted(SoundCueOptions? options = null)
    {
        Play(SoundCueKind.RecordingStarted, options);
    }

    public void PlayTranscribingStarted(SoundCueOptions? options = null)
    {
        Play(SoundCueKind.TranscribingStarted, options);
    }

    public void PlayCompleted(SoundCueOptions? options = null)
    {
        Play(SoundCueKind.Completed, options);
    }

    public void PlayError(SoundCueOptions? options = null)
    {
        Play(SoundCueKind.Error, options);
    }

    public void Play(SoundCueKind kind, SoundCueOptions? options = null)
    {
        var resolvedOptions = options ?? SoundCueOptions.Default;
        if (!resolvedOptions.Enabled || resolvedOptions.Volume <= 0)
        {
            return;
        }

        var path = ResolvePath(kind);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            _player.Play(path, ResolvePlaybackVolume(kind, resolvedOptions));
        }
        catch
        {
        }
    }

    public string ResolvePath(SoundCueKind kind)
    {
        return Path.Combine(_soundRoot, GetFileName(kind));
    }

    public static string GetFileName(SoundCueKind kind)
    {
        return kind switch
        {
            SoundCueKind.RecordingStarted => "recstart.mp3",
            SoundCueKind.TranscribingStarted => "recstop.mp3",
            SoundCueKind.Completed => "pastess.mp3",
            SoundCueKind.Error => "esc.wav",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sound cue.")
        };
    }

    public static float GetCueGain(SoundCueKind kind)
    {
        return kind switch
        {
            SoundCueKind.RecordingStarted => 0.8f,
            SoundCueKind.TranscribingStarted => 1.0f,
            SoundCueKind.Completed => 0.8f,
            SoundCueKind.Error => 0.55f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sound cue.")
        };
    }

    public static float ResolvePlaybackVolume(SoundCueKind kind, SoundCueOptions options)
    {
        return (float)Math.Clamp(options.Volume * GetCueGain(kind), 0, 1);
    }

    private sealed class NAudioSoundCuePlayer : ISoundCuePlayer
    {
        private readonly object _syncRoot = new();
        private readonly List<(WaveOutEvent Output, AudioFileReader Reader)> _activePlayers = [];

        public void Play(string path, float volume)
        {
            var reader = new AudioFileReader(path)
            {
                Volume = volume
            };
            var output = new WaveOutEvent();
            output.Init(reader);
            output.PlaybackStopped += (_, _) => DisposePlayer(output, reader);

            lock (_syncRoot)
            {
                _activePlayers.Add((output, reader));
            }

            output.Play();
        }

        private void DisposePlayer(WaveOutEvent output, AudioFileReader reader)
        {
            lock (_syncRoot)
            {
                _activePlayers.RemoveAll(player => ReferenceEquals(player.Output, output));
            }

            output.Dispose();
            reader.Dispose();
        }
    }
}
