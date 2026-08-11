using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class MultichannelProbeRetryScheduleTests
{
    [Fact]
    public void UninformativeEndpointProbeRetriesWithBoundedBackoff()
    {
        var schedule = new MultichannelProbeRetrySchedule();
        var now = DateTimeOffset.UtcNow;
        var fallback = CreateStatus(MultichannelCaptureState.Uninformative);

        schedule.ObserveStatus(fallback, now);

        Assert.Equal(now + MultichannelProbeRetrySchedule.InitialRetryDelay, schedule.NextRetryAt);
        Assert.False(schedule.TryBeginRetry(schedule.NextRetryAt!.Value - TimeSpan.FromMilliseconds(1)));
        Assert.True(schedule.TryBeginRetry(schedule.NextRetryAt.Value));

        now += MultichannelProbeRetrySchedule.InitialRetryDelay;
        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Probing), now);
        schedule.ObserveStatus(fallback, now);
        Assert.Equal(now + TimeSpan.FromMinutes(1), schedule.NextRetryAt);

        for (var expectedMinutes = 2; expectedMinutes <= 4; expectedMinutes *= 2)
        {
            now = schedule.NextRetryAt!.Value;
            Assert.True(schedule.TryBeginRetry(now));
            schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Probing), now);
            schedule.ObserveStatus(fallback, now);
            Assert.Equal(now + TimeSpan.FromMinutes(expectedMinutes), schedule.NextRetryAt);
        }

        now = schedule.NextRetryAt!.Value;
        Assert.True(schedule.TryBeginRetry(now));
        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Probing), now);
        schedule.ObserveStatus(fallback, now);
        Assert.Equal(now + MultichannelProbeRetrySchedule.MaximumRetryDelay, schedule.NextRetryAt);
    }

    [Fact]
    public void VerificationOrNewStereoSessionClearsTheRetryAndBackoff()
    {
        var schedule = new MultichannelProbeRetrySchedule();
        var now = DateTimeOffset.UtcNow;
        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Uninformative), now);
        Assert.True(schedule.TryBeginRetry(now + MultichannelProbeRetrySchedule.InitialRetryDelay));
        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Uninformative), now);

        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Verified, processId: 42), now);

        Assert.Null(schedule.NextRetryAt);
        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Uninformative), now);
        Assert.Equal(now + MultichannelProbeRetrySchedule.InitialRetryDelay, schedule.NextRetryAt);

        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.NotAttempted), now);
        Assert.Null(schedule.NextRetryAt);
    }

    [Fact]
    public void ManualProcessFallbackDoesNotScheduleAnOpportunisticRetry()
    {
        var schedule = new MultichannelProbeRetrySchedule();

        schedule.ObserveStatus(
            CreateStatus(MultichannelCaptureState.Uninformative, processId: 42),
            DateTimeOffset.UtcNow);

        Assert.Null(schedule.NextRetryAt);
    }

    [Fact]
    public void UnavailableForcedSourceRetriesWhileEndpointFallbackIsActive()
    {
        var schedule = new MultichannelProbeRetrySchedule();
        var now = DateTimeOffset.UtcNow;
        var status = CreateStatus(MultichannelCaptureState.Unavailable) with
        {
            IsMultichannelSourceForced = true
        };

        schedule.ObserveStatus(status, now);

        Assert.Equal(now + MultichannelProbeRetrySchedule.InitialRetryDelay, schedule.NextRetryAt);
    }

    [Fact]
    public void BusyCaptureDefersADueRetryBriefly()
    {
        var schedule = new MultichannelProbeRetrySchedule();
        var now = DateTimeOffset.UtcNow;
        schedule.ObserveStatus(CreateStatus(MultichannelCaptureState.Uninformative), now);
        now += MultichannelProbeRetrySchedule.InitialRetryDelay;
        Assert.True(schedule.TryBeginRetry(now));

        schedule.DeferRetry(now);

        Assert.Equal(now + MultichannelProbeRetrySchedule.DeferredRetryDelay, schedule.NextRetryAt);
    }

    private static AudioCaptureStatus CreateStatus(
        MultichannelCaptureState state,
        int? processId = null) => new(
            processId.HasValue ? "Game: Test" : "Headphones",
            processId.HasValue ? null : "endpoint",
            processId,
            "48000Hz 32-bit IEEE float",
            state == MultichannelCaptureState.Verified
                ? AudioEstimatorMode.Multichannel
                : AudioEstimatorMode.Stereo,
            state,
            RequestedLayout: "7.1",
            ObservedLayout: "7.1",
            MultichannelProcessName: "Test",
            FallbackReason: state is MultichannelCaptureState.Uninformative or MultichannelCaptureState.Unavailable
                ? "No useful side/rear content."
                : null);
}
