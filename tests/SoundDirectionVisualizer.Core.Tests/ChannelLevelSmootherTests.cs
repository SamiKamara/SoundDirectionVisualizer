using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class ChannelLevelSmootherTests
{
    [Fact]
    public void SmoothsEveryChannelAndResetRemovesThePreviousLayoutState()
    {
        var smoother = new ChannelLevelSmoother();
        var first = new ChannelLevels(ChannelLayout.Surround51, [1, 0, 0, 0, 0.5, 0]);
        var second = new ChannelLevels(ChannelLayout.Surround51, [0, 1, 0, 0, 0, 0.5]);

        smoother.Update(first, 0.25);
        var smoothed = smoother.Update(second, 0.25);

        Assert.Equal(0.75, smoothed.GetRms(SpeakerPosition.FrontLeft), precision: 6);
        Assert.Equal(0.25, smoothed.GetRms(SpeakerPosition.FrontRight), precision: 6);
        Assert.Equal(0.375, smoothed.GetRms(SpeakerPosition.SideLeft), precision: 6);
        Assert.Equal(0.125, smoothed.GetRms(SpeakerPosition.SideRight), precision: 6);

        smoother.Reset();
        var reset = smoother.Update(second, 0.25);
        Assert.Equal(0, reset.GetRms(SpeakerPosition.FrontLeft), precision: 6);
        Assert.Equal(1, reset.GetRms(SpeakerPosition.FrontRight), precision: 6);
    }
}
