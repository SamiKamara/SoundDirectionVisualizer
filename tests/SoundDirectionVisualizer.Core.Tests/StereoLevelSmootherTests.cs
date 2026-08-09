using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class StereoLevelSmootherTests
{
    [Fact]
    public void UsesInputDirectlyForFirstFrameAndExponentialBlendAfterward()
    {
        var smoother = new StereoLevelSmoother();

        var first = smoother.Update(new StereoLevels(0.2, 0.8), 0.2);
        var second = smoother.Update(new StereoLevels(0.7, 0.3), 0.2);

        Assert.Equal(new StereoLevels(0.2, 0.8), first);
        Assert.Equal(0.3, second.LeftRms, precision: 6);
        Assert.Equal(0.7, second.RightRms, precision: 6);
    }
}
