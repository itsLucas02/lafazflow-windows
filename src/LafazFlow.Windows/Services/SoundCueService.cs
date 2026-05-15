using System.Media;

namespace LafazFlow.Windows.Services;

public sealed class SoundCueService
{
    public void PlayRecordingStarted()
    {
        Play(SystemSounds.Asterisk);
    }

    public void PlayTranscribingStarted()
    {
        Play(SystemSounds.Beep);
    }

    public void PlayCompleted()
    {
        Play(SystemSounds.Exclamation);
    }

    public void PlayError()
    {
        Play(SystemSounds.Hand);
    }

    private static void Play(SystemSound sound)
    {
        try
        {
            sound.Play();
        }
        catch
        {
        }
    }
}
