using System.IO;
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

public sealed class SoundCueService
{
    private const float DefaultVolume = 0.4f;
    private const float ErrorVolume = 0.3f;

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

    public void PlayRecordingStarted()
    {
        Play(SoundCueKind.RecordingStarted);
    }

    public void PlayTranscribingStarted()
    {
        Play(SoundCueKind.TranscribingStarted);
    }

    public void PlayCompleted()
    {
        Play(SoundCueKind.Completed);
    }

    public void PlayError()
    {
        Play(SoundCueKind.Error);
    }

    public void Play(SoundCueKind kind)
    {
        var path = ResolvePath(kind);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            _player.Play(path, kind == SoundCueKind.Error ? ErrorVolume : DefaultVolume);
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
