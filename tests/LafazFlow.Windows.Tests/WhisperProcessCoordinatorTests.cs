using System.Diagnostics;
using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class WhisperProcessCoordinatorTests
{
    [Theory]
    [InlineData(WhisperWorkload.LivePreview)]
    [InlineData(WhisperWorkload.Diagnostic)]
    public async Task FinalTranscriptionCancelsInterruptibleWorkAndRunsExclusively(WhisperWorkload workload)
    {
        var coordinator = new WhisperProcessCoordinator();
        var preview = coordinator.RunAsync(
            workload,
            PowerShell("Start-Sleep -Seconds 30"),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
        await Task.Delay(300);

        var stopwatch = Stopwatch.StartNew();
        var final = await coordinator.RunAsync(
            WhisperWorkload.FinalTranscription,
            PowerShell("Write-Output final"),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview);
        Assert.Equal(0, final.ExitCode);
        Assert.Contains("final", final.StandardOutput);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TimedOutProcessIsTerminatedAndReleasesCoordinator()
    {
        var coordinator = new WhisperProcessCoordinator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.RunAsync(
            WhisperWorkload.Diagnostic,
            PowerShell("Start-Sleep -Seconds 30"),
            TimeSpan.FromMilliseconds(750),
            CancellationToken.None));

        var next = await coordinator.RunAsync(
            WhisperWorkload.FinalTranscription,
            PowerShell("Write-Output recovered"),
            TimeSpan.FromSeconds(15),
            CancellationToken.None);
        Assert.Equal(0, next.ExitCode);
        Assert.Contains("recovered", next.StandardOutput);
    }

    private static ProcessStartInfo PowerShell(string command)
    {
        return new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }
}
