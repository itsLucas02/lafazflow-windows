using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class DictationQueueProcessorTests
{
    [Fact]
    public async Task EnqueueProcessesJobsSequentiallyInOrder()
    {
        var startedFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var activeJobs = 0;
        var maxActiveJobs = 0;
        var processor = new DictationQueueProcessor(async (job, _) =>
        {
            activeJobs++;
            maxActiveJobs = Math.Max(maxActiveJobs, activeJobs);
            order.Add($"start:{job.AudioPath}");

            if (job.AudioPath == "first.wav")
            {
                startedFirst.SetResult();
                await releaseFirst.Task;
            }

            order.Add($"end:{job.AudioPath}");
            activeJobs--;
        });

        var first = processor.Enqueue(new DictationJob("first.wav", (IntPtr)1, AppSettingsFactory.Default));
        await startedFirst.Task;
        var second = processor.Enqueue(new DictationJob("second.wav", (IntPtr)2, AppSettingsFactory.Default));

        releaseFirst.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(
            ["start:first.wav", "end:first.wav", "start:second.wav", "end:second.wav"],
            order);
        Assert.Equal(1, maxActiveJobs);
    }

    [Fact]
    public async Task FailedJobDoesNotBlockLaterJobs()
    {
        var order = new List<string>();
        var processor = new DictationQueueProcessor((job, _) =>
        {
            order.Add(job.AudioPath);
            if (job.AudioPath == "bad.wav")
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        });

        await processor.Enqueue(new DictationJob("bad.wav", IntPtr.Zero, AppSettingsFactory.Default));
        await processor.Enqueue(new DictationJob("good.wav", IntPtr.Zero, AppSettingsFactory.Default));

        Assert.Equal(["bad.wav", "good.wav"], order);
    }

    private static class AppSettingsFactory
    {
        public static Core.AppSettings Default => Core.AppSettings.Default;
    }
}
