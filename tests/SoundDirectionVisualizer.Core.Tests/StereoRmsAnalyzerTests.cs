using System.Buffers.Binary;
using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class StereoRmsAnalyzerTests
{
    [Fact]
    public void CalculatesFloat32StereoRms()
    {
        var samples = new[] { 1f, 0.5f, -1f, -0.5f };
        var bytes = new byte[samples.Length * sizeof(float)];

        for (var index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(index * sizeof(float), sizeof(float)),
                BitConverter.SingleToInt32Bits(samples[index]));
        }

        var result = StereoRmsAnalyzer.Calculate(bytes, 2, StereoSampleEncoding.Float32);

        Assert.Equal(1, result.LeftRms, precision: 6);
        Assert.Equal(0.5, result.RightRms, precision: 6);
    }

    [Fact]
    public void CalculatesPcm16StereoRms()
    {
        short[] samples = [16384, -8192, -16384, 8192];
        var bytes = new byte[samples.Length * sizeof(short)];

        for (var index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short), sizeof(short)), samples[index]);
        }

        var result = StereoRmsAnalyzer.Calculate(bytes, 2, StereoSampleEncoding.Pcm16);

        Assert.Equal(0.5, result.LeftRms, precision: 6);
        Assert.Equal(0.25, result.RightRms, precision: 6);
    }

    [Fact]
    public void CalculatesPcm24StereoRmsIncludingNegativeSamples()
    {
        int[] samples = [4194304, 2097152, -4194304, -2097152];
        var bytes = new byte[samples.Length * 3];

        for (var index = 0; index < samples.Length; index++)
        {
            WritePcm24(bytes.AsSpan(index * 3, 3), samples[index]);
        }

        var result = StereoRmsAnalyzer.Calculate(bytes, 2, StereoSampleEncoding.Pcm24);

        Assert.Equal(0.5, result.LeftRms, precision: 6);
        Assert.Equal(0.25, result.RightRms, precision: 6);
    }

    [Fact]
    public void CalculatesPcm32StereoRms()
    {
        int[] samples = [1073741824, 536870912, -1073741824, -536870912];
        var bytes = new byte[samples.Length * sizeof(int)];

        for (var index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * sizeof(int), sizeof(int)), samples[index]);
        }

        var result = StereoRmsAnalyzer.Calculate(bytes, 2, StereoSampleEncoding.Pcm32);

        Assert.Equal(0.5, result.LeftRms, precision: 6);
        Assert.Equal(0.25, result.RightRms, precision: 6);
    }

    [Fact]
    public void RejectsNonStereoInputInVersionOne()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            StereoRmsAnalyzer.Calculate(new byte[16], 6, StereoSampleEncoding.Float32));

        Assert.Contains("exactly two channels", exception.Message);
    }

    private static void WritePcm24(Span<byte> destination, int value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
    }
}
