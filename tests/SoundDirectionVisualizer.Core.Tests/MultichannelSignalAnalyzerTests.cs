using System.Buffers.Binary;
using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class MultichannelSignalAnalyzerTests
{
    [Fact]
    public void CalculatesAll51FloatChannelsAndEnergyPreservingStereoFallback()
    {
        float[][] frames =
        [
            [1f, 0.5f, 0.25f, 0.125f, 0.75f, 0.625f],
            [-1f, -0.5f, -0.25f, -0.125f, -0.75f, -0.625f]
        ];

        var result = MultichannelSignalAnalyzer.Calculate(
            ToFloatBytes(frames),
            ChannelLayout.Surround51,
            StereoSampleEncoding.Float32);

        Assert.Equal(1, result.Levels.GetRms(SpeakerPosition.FrontLeft), precision: 6);
        Assert.Equal(0.5, result.Levels.GetRms(SpeakerPosition.FrontRight), precision: 6);
        Assert.Equal(0.25, result.Levels.GetRms(SpeakerPosition.FrontCenter), precision: 6);
        Assert.Equal(0.125, result.Levels.GetRms(SpeakerPosition.LowFrequency), precision: 6);
        Assert.Equal(0.75, result.Levels.GetRms(SpeakerPosition.SideLeft), precision: 6);
        Assert.Equal(0.625, result.Levels.GetRms(SpeakerPosition.SideRight), precision: 6);
        Assert.Equal(Math.Sqrt(1 + 0.5625 + 0.03125), result.StereoFallbackLevels.LeftRms, precision: 6);
        Assert.Equal(Math.Sqrt(0.25 + 0.390625 + 0.03125), result.StereoFallbackLevels.RightRms, precision: 6);
    }

    [Fact]
    public void CalculatesEvery71Pcm16ChannelInsteadOfReadingOnlyTheFirstTwo()
    {
        short[][] frames =
        [
            [16384, 8192, 4096, 2048, 12288, 10240, 6144, 5120],
            [-16384, -8192, -4096, -2048, -12288, -10240, -6144, -5120]
        ];

        var result = MultichannelSignalAnalyzer.Calculate(
            ToPcm16Bytes(frames),
            ChannelLayout.Surround71,
            StereoSampleEncoding.Pcm16);

        Assert.Equal(0.5, result.Levels.GetRms(SpeakerPosition.FrontLeft), precision: 6);
        Assert.Equal(0.375, result.Levels.GetRms(SpeakerPosition.BackLeft), precision: 6);
        Assert.Equal(0.3125, result.Levels.GetRms(SpeakerPosition.BackRight), precision: 6);
        Assert.Equal(0.1875, result.Levels.GetRms(SpeakerPosition.SideLeft), precision: 6);
        Assert.Equal(0.15625, result.Levels.GetRms(SpeakerPosition.SideRight), precision: 6);
    }

    [Fact]
    public void IdentifiesStereoDerivedSurroundChannelsAsNonIndependent()
    {
        var frames = CreateCorrelationFixture((left, right, _) =>
            (SideLeft: (0.5f * left) + (0.25f * right), SideRight: (-0.2f * left) + (0.75f * right)));

        var result = MultichannelSignalAnalyzer.Calculate(
            ToFloatBytes(frames),
            ChannelLayout.Surround51,
            StereoSampleEncoding.Float32);

        Assert.Equal(0, result.GetIndependenceRatio(SpeakerPosition.SideLeft), precision: 6);
        Assert.Equal(0, result.GetIndependenceRatio(SpeakerPosition.SideRight), precision: 6);
    }

    [Fact]
    public void IdentifiesARearWaveformOrthogonalToTheStereoFrontAsIndependent()
    {
        float[] independent = [1, 1, 1, 1, -1, -1, -1, -1];
        var frames = CreateCorrelationFixture((left, _, index) =>
            (SideLeft: left, SideRight: independent[index]));

        var result = MultichannelSignalAnalyzer.Calculate(
            ToFloatBytes(frames),
            ChannelLayout.Surround51,
            StereoSampleEncoding.Float32);

        Assert.Equal(0, result.GetIndependenceRatio(SpeakerPosition.SideLeft), precision: 6);
        Assert.Equal(1, result.GetIndependenceRatio(SpeakerPosition.SideRight), precision: 6);
    }

    [Fact]
    public void DoesNotMistakeACopiedFrontCenterChannelForIndependentSurroundContent()
    {
        float[][] frames =
        [
            [0, 0, 1, 0, 1, 1],
            [0, 0, -0.5f, 0, -0.5f, -0.5f],
            [0, 0, 0.25f, 0, 0.25f, 0.25f],
            [0, 0, -1, 0, -1, -1]
        ];

        var result = MultichannelSignalAnalyzer.Calculate(
            ToFloatBytes(frames),
            ChannelLayout.Surround51,
            StereoSampleEncoding.Float32);

        Assert.Equal(0, result.GetIndependenceRatio(SpeakerPosition.SideLeft), precision: 6);
        Assert.Equal(0, result.GetIndependenceRatio(SpeakerPosition.SideRight), precision: 6);
    }

    [Fact]
    public void RejectsAMalformedPartialMultichannelFrame()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            MultichannelSignalAnalyzer.Calculate(
                new byte[ChannelLayout.Surround71.ChannelCount * sizeof(float) - 1],
                ChannelLayout.Surround71,
                StereoSampleEncoding.Float32));

        Assert.Contains("whole number of 7.1 frames", exception.Message);
    }

    private static float[][] CreateCorrelationFixture(
        Func<float, float, int, (float SideLeft, float SideRight)> createSurround)
    {
        float[] left = [1, 0, -1, 0, 1, 0, -1, 0];
        float[] right = [0, 1, 0, -1, 0, 1, 0, -1];
        var frames = new float[left.Length][];

        for (var index = 0; index < frames.Length; index++)
        {
            var surround = createSurround(left[index], right[index], index);
            frames[index] = [left[index], right[index], 0, 0, surround.SideLeft, surround.SideRight];
        }

        return frames;
    }

    private static byte[] ToFloatBytes(IReadOnlyList<float[]> frames)
    {
        var channelCount = frames[0].Length;
        var bytes = new byte[frames.Count * channelCount * sizeof(float)];
        for (var frame = 0; frame < frames.Count; frame++)
        {
            for (var channel = 0; channel < channelCount; channel++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan(((frame * channelCount) + channel) * sizeof(float), sizeof(float)),
                    BitConverter.SingleToInt32Bits(frames[frame][channel]));
            }
        }

        return bytes;
    }

    private static byte[] ToPcm16Bytes(IReadOnlyList<short[]> frames)
    {
        var channelCount = frames[0].Length;
        var bytes = new byte[frames.Count * channelCount * sizeof(short)];
        for (var frame = 0; frame < frames.Count; frame++)
        {
            for (var channel = 0; channel < channelCount; channel++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(
                    bytes.AsSpan(((frame * channelCount) + channel) * sizeof(short), sizeof(short)),
                    frames[frame][channel]);
            }
        }

        return bytes;
    }
}
