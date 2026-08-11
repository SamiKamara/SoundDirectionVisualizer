using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class MultichannelDirectionEstimatorTests
{
    [Theory]
    [InlineData(SpeakerPosition.FrontLeft, 330)]
    [InlineData(SpeakerPosition.FrontRight, 30)]
    [InlineData(SpeakerPosition.FrontCenter, 0)]
    [InlineData(SpeakerPosition.BackLeft, 210)]
    [InlineData(SpeakerPosition.BackRight, 150)]
    [InlineData(SpeakerPosition.SideLeft, 270)]
    [InlineData(SpeakerPosition.SideRight, 90)]
    public void MapsSingle71SpeakerEnergyToItsNominalAzimuth(
        SpeakerPosition position,
        double expectedAzimuth)
    {
        var levels = CreateLevels(ChannelLayout.Surround71, (position, 1));

        var result = MultichannelDirectionEstimator.Estimate(levels);

        Assert.False(result.IsQuiet);
        Assert.Single(result.CandidateAzimuths);
        Assert.Equal(expectedAzimuth, result.CandidateAzimuths[0], precision: 6);
    }

    [Fact]
    public void InterpolatesAnEqualEnergyMixtureBetweenAdjacentSpeakers()
    {
        var levels = CreateLevels(
            ChannelLayout.Surround71,
            (SpeakerPosition.FrontRight, 1),
            (SpeakerPosition.SideRight, 1));

        var result = MultichannelDirectionEstimator.Estimate(levels);

        Assert.Single(result.CandidateAzimuths);
        Assert.Equal(60, result.CandidateAzimuths[0], precision: 6);
    }

    [Fact]
    public void PreservesOpposingFrontBackUncertaintyInsteadOfInventingAPreciseAverage()
    {
        var layout = new ChannelLayout(
            "front/back test",
            SpeakerPosition.FrontLeft,
            SpeakerPosition.FrontRight,
            SpeakerPosition.FrontCenter,
            SpeakerPosition.BackCenter);
        var levels = CreateLevels(
            layout,
            (SpeakerPosition.FrontCenter, 1),
            (SpeakerPosition.BackCenter, 1));

        var result = MultichannelDirectionEstimator.Estimate(levels);

        Assert.Equal([0d, 180d], result.CandidateAzimuths);
    }

    [Fact]
    public void IgnoresLfeAsADirectionalSpeaker()
    {
        var levels = CreateLevels(ChannelLayout.Surround51, (SpeakerPosition.LowFrequency, 1));

        var result = MultichannelDirectionEstimator.Estimate(levels);

        Assert.True(result.IsQuiet);
        Assert.Empty(result.CandidateAzimuths);
    }

    [Fact]
    public void Keeps51SideChannelsDistinctFromFrontBackStereoAmbiguity()
    {
        var levels = CreateLevels(ChannelLayout.Surround51, (SpeakerPosition.SideLeft, 1));

        var result = MultichannelDirectionEstimator.Estimate(levels);

        Assert.Equal([270d], result.CandidateAzimuths);
    }

    private static ChannelLevels CreateLevels(
        ChannelLayout layout,
        params (SpeakerPosition Position, double Rms)[] values)
    {
        var levels = new double[layout.ChannelCount];
        foreach (var value in values)
        {
            levels[layout.IndexOf(value.Position)] = value.Rms;
        }

        return new ChannelLevels(layout, levels);
    }
}
