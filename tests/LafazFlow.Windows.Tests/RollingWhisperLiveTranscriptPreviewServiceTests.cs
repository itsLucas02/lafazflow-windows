using LafazFlow.Windows.Core;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class RollingWhisperLiveTranscriptPreviewServiceTests
{
    [Fact]
    public async Task AcceptedPreviewsFlowToCallback()
    {
        var service = CreateService(
            previews: ["Testing one two."],
            logs: out _);
        var received = new List<string>();

        await service.StartAsync(AppSettings.Default, received.Add, CancellationToken.None);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await WaitUntilAsync(() => received.Count == 1);
        await service.StopAsync();

        Assert.Equal(["Testing one two."], received);
    }

    [Fact]
    public async Task SkipsPreviewWhenNotEnoughNewAudioArrived()
    {
        var calls = 0;
        var service = new RollingWhisperLiveTranscriptPreviewService(
            TestOptions(),
            (_, _, _, _) =>
            {
                calls++;
                return Task.FromResult("Testing one two.");
            },
            _ => { });

        await service.StartAsync(AppSettings.Default, _ => { }, CancellationToken.None);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await WaitUntilAsync(() => calls == 1);
        await Task.Delay(90);
        await service.StopAsync();

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SuppressionsAreAggregatedIntoOneSummary()
    {
        var service = CreateService(
            previews:
            [
                "Testing testing one two three.",
                "Testing testing one two three.",
                "Testing"
            ],
            logs: out var logs);

        await service.StartAsync(AppSettings.Default, _ => { }, CancellationToken.None);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await Task.Delay(35);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await Task.Delay(35);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await Task.Delay(80);
        await service.StopAsync();

        var summary = Assert.Single(logs);
        Assert.Contains("Live preview summary:", summary);
        Assert.Contains("accepted=1", summary);
        Assert.Contains("duplicate=1", summary);
        Assert.Contains("regressive=1", summary);
        Assert.DoesNotContain("Live preview suppressed:", summary);
    }

    [Fact]
    public async Task StopClearsSessionBeforeRestart()
    {
        var service = CreateService(
            previews:
            [
                "Testing one two.",
                "Testing one two."
            ],
            logs: out _);
        var received = new List<string>();

        await service.StartAsync(AppSettings.Default, received.Add, CancellationToken.None);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await WaitUntilAsync(() => received.Count == 1);
        await service.StopAsync();

        await service.StartAsync(AppSettings.Default, received.Add, CancellationToken.None);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await WaitUntilAsync(() => received.Count == 2);
        await service.StopAsync();

        Assert.Equal(["Testing one two.", "Testing one two."], received);
    }

    [Fact]
    public async Task ContinuesPreviewingAfterRollingWindowIsFull()
    {
        var calls = 0;
        var service = new RollingWhisperLiveTranscriptPreviewService(
            TestOptions(),
            (_, _, _, _) =>
            {
                calls++;
                return Task.FromResult($"Testing {calls}.");
            },
            _ => { });

        await service.StartAsync(AppSettings.Default, _ => { }, CancellationToken.None);
        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 600));
        await WaitUntilAsync(() => calls == 1);

        service.AcceptAudioChunk(CreatePcmChunk(milliseconds: 80));
        await WaitUntilAsync(() => calls == 2);
        await service.StopAsync();

        Assert.Equal(2, calls);
    }

    private static RollingWhisperLiveTranscriptPreviewService CreateService(
        IReadOnlyCollection<string> previews,
        out List<string> logs)
    {
        var queue = new Queue<string>(previews);
        logs = [];
        var capturedLogs = logs;
        return new RollingWhisperLiveTranscriptPreviewService(
            TestOptions(),
            (_, _, _, _) => Task.FromResult(queue.Count > 0 ? queue.Dequeue() : ""),
            capturedLogs.Add);
    }

    private static RollingWhisperLiveTranscriptPreviewOptions TestOptions() => new()
    {
        PreviewIntervalMilliseconds = 20,
        MinimumAudioMilliseconds = 20,
        RollingWindowMilliseconds = 500,
        MinimumNewAudioMilliseconds = 60
    };

    private static byte[] CreatePcmChunk(int milliseconds)
    {
        var byteCount = 16000 * 2 * milliseconds / 1000;
        return Enumerable.Repeat((byte)1, byteCount).ToArray();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }
}
