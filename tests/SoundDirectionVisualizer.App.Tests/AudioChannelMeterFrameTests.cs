using SoundDirectionVisualizer.App.Services;
using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class AudioChannelMeterFrameTests
{
    [Fact]
    public void MultichannelFactoryPreservesEverySevenPointOneChannelInLayoutOrder()
    {
        var levels = new ChannelLevels(
            ChannelLayout.Surround71,
            [0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1]);

        var frame = AudioChannelMeterFrameFactory.FromMultichannel(
            DateTimeOffset.UnixEpoch,
            "Game: Test",
            levels);

        Assert.Equal("7.1", frame.LayoutName);
        Assert.Equal("Game: Test", frame.SourceName);
        Assert.Equal(
            ["FL", "FR", "FC", "LFE", "BL", "BR", "SL", "SR"],
            frame.Channels.Select(channel => channel.ShortLabel));
        Assert.Equal(
            [0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1],
            frame.Channels.Select(channel => channel.RmsLevel));
        Assert.Contains(
            frame.Channels,
            channel => channel.Position == SpeakerPosition.LowFrequency
                && channel.DisplayName == "Low-frequency effects");
    }

    [Fact]
    public void StereoFactoryExposesBothMonitoredEndpointChannels()
    {
        var frame = AudioChannelMeterFrameFactory.FromStereo(
            DateTimeOffset.UnixEpoch,
            "Headphones",
            new StereoLevels(0.25, 0.5));

        Assert.Equal("Stereo", frame.LayoutName);
        Assert.Equal(["FL", "FR"], frame.Channels.Select(channel => channel.ShortLabel));
        Assert.Equal([0.25, 0.5], frame.Channels.Select(channel => channel.RmsLevel));
    }

    [Fact]
    public void FrameCopiesItsChannelCollection()
    {
        var channels = new[]
        {
            new AudioChannelMeterChannel(SpeakerPosition.FrontLeft, "FL", "Front left", 0.25)
        };
        var frame = new AudioChannelMeterFrame(
            DateTimeOffset.UnixEpoch,
            "Source",
            "Layout",
            channels);

        channels[0] = new AudioChannelMeterChannel(
            SpeakerPosition.FrontRight,
            "FR",
            "Front right",
            1);

        Assert.Equal(SpeakerPosition.FrontLeft, frame.Channels[0].Position);
        Assert.Equal(0.25, frame.Channels[0].RmsLevel);
    }

    [Theory]
    [InlineData(0, double.NegativeInfinity, 0)]
    [InlineData(0.001, -60, 0)]
    [InlineData(0.03162277660168379, -30, 0.5)]
    [InlineData(1, 0, 1)]
    public void MeterScaleUsesLogarithmicDbfsWidth(
        double rmsLevel,
        double expectedDecibels,
        double expectedWidth)
    {
        var decibels = AudioChannelMeterScale.ToDecibels(rmsLevel);

        if (double.IsNegativeInfinity(expectedDecibels))
        {
            Assert.True(double.IsNegativeInfinity(decibels));
        }
        else
        {
            Assert.Equal(expectedDecibels, decibels, precision: 6);
        }

        Assert.Equal(expectedWidth, AudioChannelMeterScale.ToNormalizedWidth(rmsLevel), precision: 6);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ChannelRejectsInvalidRmsLevels(double rmsLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioChannelMeterChannel(
            SpeakerPosition.FrontLeft,
            "FL",
            "Front left",
            rmsLevel));
    }
}
