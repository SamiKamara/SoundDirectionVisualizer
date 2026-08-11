using System.Buffers.Binary;

namespace SoundDirectionVisualizer.Core.Audio;

internal static class AudioSampleDecoder
{
    public static int GetBytesPerSample(StereoSampleEncoding encoding) => encoding switch
    {
        StereoSampleEncoding.Float32 => 4,
        StereoSampleEncoding.Pcm16 => 2,
        StereoSampleEncoding.Pcm24 => 3,
        StereoSampleEncoding.Pcm32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    public static double ReadSample(ReadOnlySpan<byte> sample, StereoSampleEncoding encoding)
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
