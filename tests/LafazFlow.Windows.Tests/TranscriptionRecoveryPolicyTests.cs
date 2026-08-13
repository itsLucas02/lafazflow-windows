using LafazFlow.Windows.Services;

namespace LafazFlow.Windows.Tests;

public sealed class TranscriptionRecoveryPolicyTests
{
    [Theory]
    [InlineData("aborted")]
    [InlineData("worker_unavailable")]
    [InlineData("worker_timeout")]
    [InlineData("worker_busy")]
    [InlineData("worker_internalerror")]
    [InlineData("pipe_broken")]
    public void RetryableFailuresRequestWorkerRestart(string failureKind)
    {
        Assert.Equal(
            TranscriptionRecoveryAction.RetryWorker,
            TranscriptionRecoveryPolicy.Decide(failureKind, userCancelled: false, deliveryCommitted: false));
    }

    [Theory]
    [InlineData("invalid_audio")]
    [InlineData("model_missing")]
    [InlineData("vad_missing")]
    [InlineData("invalid_settings")]
    [InlineData(null)]
    public void NonRetryableFailuresDoNotRestart(string? failureKind)
    {
        Assert.Equal(
            TranscriptionRecoveryAction.None,
            TranscriptionRecoveryPolicy.Decide(failureKind, userCancelled: false, deliveryCommitted: false));
    }

    [Fact]
    public void UserCancellationIsNeverRetried()
    {
        Assert.Equal(
            TranscriptionRecoveryAction.None,
            TranscriptionRecoveryPolicy.Decide("worker_unavailable", userCancelled: true, deliveryCommitted: false));
    }

    [Fact]
    public void CommittedDeliveryIsNeverRetried()
    {
        Assert.Equal(
            TranscriptionRecoveryAction.None,
            TranscriptionRecoveryPolicy.Decide("worker_unavailable", userCancelled: false, deliveryCommitted: true));
    }
}
