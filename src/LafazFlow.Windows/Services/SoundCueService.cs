using System.IO;
using LafazFlow.Windows.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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

public readonly record struct SoundCueOptions(
    bool Enabled,
    float Volume,
    float RecordingStartedVolume,
    float TranscribingStartedVolume,
    float CompletedVolume,
    float ErrorVolume)
{
    public SoundCueOptions(bool enabled, float volume)
        : this(enabled, volume, 1.0f, 1.0f, 1.45f, 1.0f)
    {
    }

    public static SoundCueOptions Default { get; } = new(true, 0.5f, 1.0f, 1.0f, 1.45f, 1.0f);

    public static SoundCueOptions FromSettings(AppSettings settings)
    {
        return new SoundCueOptions(
            settings.EnableSoundCues,
            (float)Math.Clamp(settings.SoundCueVolume, 0, 1),
            (float)Math.Clamp(settings.SoundCueRecordingStartedVolume, 0, 2),
            (float)Math.Clamp(settings.SoundCueTranscribingStartedVolume, 0, 2),
            (float)Math.Clamp(settings.SoundCueCompletedVolume, 0, 2),
            (float)Math.Clamp(settings.SoundCueErrorVolume, 0, 2));
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
            SoundCueKind.RecordingStarted => "recstart.wav",
            SoundCueKind.TranscribingStarted => "recstop.wav",
            SoundCueKind.Completed => "pastess.mp3",
            SoundCueKind.Error => "esc.wav",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sound cue.")
        };
    }

    public static float GetCueVolume(SoundCueKind kind, SoundCueOptions options)
    {
        return kind switch
        {
            SoundCueKind.RecordingStarted => options.RecordingStartedVolume,
            SoundCueKind.TranscribingStarted => options.TranscribingStartedVolume,
            SoundCueKind.Completed => options.CompletedVolume,
            SoundCueKind.Error => options.ErrorVolume,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sound cue.")
        };
    }

    public static float ResolvePlaybackVolume(SoundCueKind kind, SoundCueOptions options)
    {
        return (float)Math.Clamp(options.Volume * GetCueVolume(kind, options), 0, 1);
    }

    private sealed class NAudioSoundCuePlayer : ISoundCuePlayer
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, CachedSound> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly WaveOutEvent _output;
        private readonly MixingSampleProvider _mixer;

        public NAudioSoundCuePlayer()
        {
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
            {
                ReadFully = true
            };
            _output = new WaveOutEvent
            {
                DesiredLatency = 120,
                NumberOfBuffers = 3
            };
            _output.Init(_mixer);
            _output.Play();
        }

        public void Play(string path, float volume)
        {
            lock (_syncRoot)
            {
                var sound = GetOrLoad(path);
                _mixer.AddMixerInput(new CachedSoundSampleProvider(sound, volume));

                if (_output.PlaybackState != PlaybackState.Playing)
                {
                    _output.Play();
                }
            }
        }

        private CachedSound GetOrLoad(string path)
        {
            if (_cache.TryGetValue(path, out var cachedSound))
            {
                return cachedSound;
            }

            var sound = new CachedSound(path, _mixer.WaveFormat);
            _cache[path] = sound;
            return sound;
        }
    }

    private sealed class CachedSound
    {
        public CachedSound(string path, WaveFormat expectedWaveFormat)
        {
            using var reader = new AudioFileReader(path);
            if (!WaveFormatMatches(reader.WaveFormat, expectedWaveFormat))
            {
                throw new InvalidOperationException(
                    $"Sound cue '{Path.GetFileName(path)}' must be {expectedWaveFormat.SampleRate} Hz stereo float audio.");
            }

            var wholeFile = new List<float>((int)(reader.Length / 4));
            var readBuffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int samplesRead;
            while ((samplesRead = reader.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                wholeFile.AddRange(readBuffer.Take(samplesRead));
            }

            AudioData = wholeFile.ToArray();
            WaveFormat = reader.WaveFormat;
        }

        public float[] AudioData { get; }

        public WaveFormat WaveFormat { get; }

        private static bool WaveFormatMatches(WaveFormat actual, WaveFormat expected)
        {
            return actual.SampleRate == expected.SampleRate
                && actual.Channels == expected.Channels
                && actual.Encoding == expected.Encoding;
        }
    }

    private sealed class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly CachedSound _sound;
        private readonly float _volume;
        private long _position;

        public CachedSoundSampleProvider(CachedSound sound, float volume)
        {
            _sound = sound;
            _volume = volume;
        }

        public WaveFormat WaveFormat => _sound.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var availableSamples = _sound.AudioData.Length - _position;
            var samplesToCopy = Math.Min(availableSamples, count);
            for (var index = 0; index < samplesToCopy; index++)
            {
                buffer[offset + index] = _sound.AudioData[_position + index] * _volume;
            }

            _position += samplesToCopy;
            return (int)samplesToCopy;
        }
    }
}
