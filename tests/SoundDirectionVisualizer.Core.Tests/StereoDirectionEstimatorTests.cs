using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class StereoDirectionEstimatorTests
{
    [Fact]
    public void QuietLevelsDoNotProduceDirectionCandidates()
    {
        var result = StereoDirectionEstimator.Estimate(new StereoLevels(0.0002, 0.0003));

        Assert.True(result.IsQuiet);
        Assert.Empty(result.CandidateAzimuths);
    }

    [Fact]
    public void EqualChannelsProduceFrontAndBackCandidates()
    {
        var result = StereoDirectionEstimator.Estimate(new StereoLevels(0.4, 0.4));

        Assert.False(result.IsQuiet);
        Assert.Equal(new[] { 0d, 180d }, result.CandidateAzimuths);
    }

    [Theory]
    [InlineData(0.2, 0.8, 90)]
    [InlineData(0.8, 0.2, 270)]
    public void ModelHardPanCollapsesToOneSideCandidate(double left, double right, double expected)
    {
        var result = StereoDirectionEstimator.Estimate(new StereoLevels(left, right));

        var candidate = Assert.Single(result.CandidateAzimuths);
        Assert.Equal(expected, candidate, precision: 6);
    }

    [Fact]
    public void IntermediateRightBalanceProducesMirroredFrontAndBackCandidates()
    {
        var result = StereoDirectionEstimator.Estimate(new StereoLevels(0.35, 0.65));

        Assert.Equal(30, result.CandidateAzimuths[0], precision: 6);
        Assert.Equal(150, result.CandidateAzimuths[1], precision: 6);
        Assert.Equal(0.35, result.LeftShare, precision: 6);
        Assert.Equal(0.65, result.RightShare, precision: 6);
    }
}
