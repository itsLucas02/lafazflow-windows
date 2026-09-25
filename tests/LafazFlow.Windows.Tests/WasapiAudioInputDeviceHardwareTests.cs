using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WasapiAudioInputDeviceHardwareTests
{
    [Fact]
    public async Task DefaultMicrophoneDeliversAndDrains16kPcmWhenRequested()
    {
        if (Environment.GetEnvironmentVariable("LAFAZFLOW_TEST_REAL_MIC") != "1") return;

        using var input = new WasapiAudioInputDevice();
        var firstAudio = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bytes = 0;
        input.DataAvailable += (_, args) =>
        {
            Assert.Equal(0, args.BytesRecorded % 2);
            Interlocked.Add(ref bytes, args.BytesRecorded);
            firstAudio.TrySetResult(true);
        };
        input.RecordingStopped += (_, args) => stopped.TrySetResult(args.Exception);

        input.StartRecording();
        await firstAudio.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(250);
        input.StopRecording();
        Assert.Null(await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(16000, input.WaveFormat.SampleRate);
        Assert.True(bytes > 1600);
    }
}
