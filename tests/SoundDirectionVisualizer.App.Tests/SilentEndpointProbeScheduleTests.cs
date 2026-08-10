using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class SilentEndpointProbeScheduleTests
{
    [Fact]
    public void ProbeStartsOnlyAfterTheSilenceGracePeriod()
    {
        var schedule = new SilentEndpointProbeSchedule();
        var now = DateTimeOffset.UtcNow;

        schedule.Reset(now);

        Assert.False(schedule.TryBeginProbe(now + SilentEndpointProbeSchedule.SilenceGracePeriod - TimeSpan.FromMilliseconds(1)));
        Assert.True(schedule.TryBeginProbe(now + SilentEndpointProbeSchedule.SilenceGracePeriod));
        Assert.False(schedule.TryBeginProbe(now + SilentEndpointProbeSchedule.SilenceGracePeriod));
    }

    [Fact]
    public void UnsuccessfulProbesBackOffToTheMaximumDelay()
    {
        var schedule = new SilentEndpointProbeSchedule();
        var now = DateTimeOffset.UtcNow;
        schedule.Reset(now);
        now += SilentEndpointProbeSchedule.SilenceGracePeriod;

        Assert.True(schedule.TryBeginProbe(now));
        schedule.CompleteProbe(now, sourceChanged: false);
        Assert.False(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(4)));
        Assert.True(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(5)));

        now += TimeSpan.FromSeconds(5);
        schedule.CompleteProbe(now, sourceChanged: false);
        Assert.True(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(10)));

        now += TimeSpan.FromSeconds(10);
        schedule.CompleteProbe(now, sourceChanged: false);
        Assert.True(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(20)));

        now += TimeSpan.FromSeconds(20);
        schedule.CompleteProbe(now, sourceChanged: false);
        Assert.True(schedule.TryBeginProbe(now + SilentEndpointProbeSchedule.MaximumRetryDelay));
    }

    [Fact]
    public void AudibleFrameAndSourceChangeRestoreTheGracePeriod()
    {
        var schedule = new SilentEndpointProbeSchedule();
        var now = DateTimeOffset.UtcNow;
        schedule.Reset(now);
        schedule.ObserveAudibleFrame(now + TimeSpan.FromSeconds(7));

        Assert.False(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(14)));
        Assert.True(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(15)));

        now += TimeSpan.FromSeconds(15);
        schedule.CompleteProbe(now, sourceChanged: true);
        Assert.False(schedule.TryBeginProbe(now + TimeSpan.FromSeconds(7)));
        Assert.True(schedule.TryBeginProbe(now + SilentEndpointProbeSchedule.SilenceGracePeriod));
    }
}
