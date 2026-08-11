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

        var bytesPerSample = AudioSampleDecoder.GetBytesPerSample(encoding);

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
            var left = AudioSampleDecoder.ReadSample(audioData.Slice(frameOffset, bytesPerSample), encoding);
            var right = AudioSampleDecoder.ReadSample(
                audioData.Slice(frameOffset + bytesPerSample, bytesPerSample),
                encoding);
            leftSquares += left * left;
            rightSquares += right * right;
        }

        return new StereoLevels(
            Math.Sqrt(leftSquares / frameCount),
            Math.Sqrt(rightSquares / frameCount));
    }

}
