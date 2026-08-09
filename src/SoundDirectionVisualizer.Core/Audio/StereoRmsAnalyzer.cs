using System.Buffers.Binary;

namespace SoundDirectionVisualizer.Core.Audio;

public static class StereoRmsAnalyzer
{
    public static StereoLevels Calculate(
        ReadOnlySpan<byte> audioData,
        int channels,
        StereoSampleEncoding encoding)
    {
        if (channels != 2)
        {
            throw new NotSupportedException($"Stereo analysis requires exactly two channels; received {channels}.");
        }

        var bytesPerSample = encoding switch
        {
            StereoSampleEncoding.Float32 => 4,
            StereoSampleEncoding.Pcm16 => 2,
            StereoSampleEncoding.Pcm24 => 3,
            StereoSampleEncoding.Pcm32 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(encoding))
        };

        var bytesPerFrame = bytesPerSample * channels;
        var frameCount = audioData.Length / bytesPerFrame;
        if (frameCount == 0)
        {
            return default;
        }

        double leftSquares = 0;
        double rightSquares = 0;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * bytesPerFrame;
            var left = ReadSample(audioData.Slice(frameOffset, bytesPerSample), encoding);
            var right = ReadSample(audioData.Slice(frameOffset + bytesPerSample, bytesPerSample), encoding);
            leftSquares += left * left;
            rightSquares += right * right;
        }

        return new StereoLevels(
            Math.Sqrt(leftSquares / frameCount),
            Math.Sqrt(rightSquares / frameCount));
    }

    private static double ReadSample(ReadOnlySpan<byte> sample, StereoSampleEncoding encoding)
    {
        return encoding switch
        {
            StereoSampleEncoding.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample)),
            StereoSampleEncoding.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768d,
            StereoSampleEncoding.Pcm24 => ReadPcm24(sample) / 8388608d,
            StereoSampleEncoding.Pcm32 => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648d,
            _ => throw new ArgumentOutOfRangeException(nameof(encoding))
        };
    }

    private static int ReadPcm24(ReadOnlySpan<byte> sample)
    {
        var value = sample[0] | (sample[1] << 8) | (sample[2] << 16);
        return (value & 0x00800000) != 0
            ? value | unchecked((int)0xFF000000)
            : value;
    }
}
