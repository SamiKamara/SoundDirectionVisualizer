namespace SoundDirectionVisualizer.Core.Audio;

public static class MultichannelSignalAnalyzer
{
    private const double NumericalFloor = 1e-20;

    public static MultichannelSignalAnalysis Calculate(
        ReadOnlySpan<byte> audioData,
        ChannelLayout layout,
        StereoSampleEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var bytesPerSample = AudioSampleDecoder.GetBytesPerSample(encoding);
        var bytesPerFrame = bytesPerSample * layout.ChannelCount;
        if (audioData.Length % bytesPerFrame != 0)
        {
            throw new ArgumentException(
                $"Audio buffer length {audioData.Length} is not a whole number of {layout.Name} frames ({bytesPerFrame} bytes each).",
                nameof(audioData));
        }

        var frameCount = audioData.Length / bytesPerFrame;
        var crossProducts = new double[layout.ChannelCount, layout.ChannelCount];
        Span<double> samples = stackalloc double[layout.ChannelCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * bytesPerFrame;
            for (var channel = 0; channel < layout.ChannelCount; channel++)
            {
                samples[channel] = AudioSampleDecoder.ReadSample(
                    audioData.Slice(frameOffset + (channel * bytesPerSample), bytesPerSample),
                    encoding);
            }

            for (var first = 0; first < layout.ChannelCount; first++)
            {
                for (var second = first; second < layout.ChannelCount; second++)
                {
                    var product = samples[first] * samples[second];
                    crossProducts[first, second] += product;
                    if (first != second)
                    {
                        crossProducts[second, first] += product;
                    }
                }
            }
        }

        var rmsLevels = new double[layout.ChannelCount];
        if (frameCount > 0)
        {
            for (var channel = 0; channel < layout.ChannelCount; channel++)
            {
                rmsLevels[channel] = Math.Sqrt(crossProducts[channel, channel] / frameCount);
            }
        }

        var independence = CalculateSurroundIndependence(layout, crossProducts);
        return new MultichannelSignalAnalysis(new ChannelLevels(layout, rmsLevels), independence);
    }

    private static IReadOnlyDictionary<SpeakerPosition, double> CalculateSurroundIndependence(
        ChannelLayout layout,
        double[,] crossProducts)
    {
        var result = new Dictionary<SpeakerPosition, double>();
        var referenceChannels = Enumerable.Range(0, layout.ChannelCount)
            .Where(index => !IsSurroundPosition(layout.Positions[index]))
            .Where(index => layout.Positions[index] != SpeakerPosition.LowFrequency)
            .ToArray();

        for (var target = 0; target < layout.ChannelCount; target++)
        {
            var position = layout.Positions[target];
            if (!IsSurroundPosition(position))
            {
                continue;
            }

            result[position] = CalculateResidualRatio(crossProducts, referenceChannels, target);
        }

        return result;
    }

    private static double CalculateResidualRatio(
        double[,] crossProducts,
        IReadOnlyList<int> referenceChannels,
        int target)
    {
        var targetEnergy = crossProducts[target, target];
        if (targetEnergy <= NumericalFloor)
        {
            return 0;
        }

        if (referenceChannels.Count == 0)
        {
            return 1;
        }

        var maximumReferenceEnergy = referenceChannels
            .Max(index => crossProducts[index, index]);
        if (maximumReferenceEnergy <= NumericalFloor)
        {
            return 1;
        }

        var count = referenceChannels.Count;
        var augmented = new double[count, count + 1];
        var regularization = maximumReferenceEnergy * 1e-10;
        for (var row = 0; row < count; row++)
        {
            for (var column = 0; column < count; column++)
            {
                augmented[row, column] = crossProducts[
                    referenceChannels[row],
                    referenceChannels[column]];
            }

            augmented[row, row] += regularization;
            augmented[row, count] = crossProducts[referenceChannels[row], target];
        }

        var coefficients = SolveLinearSystem(augmented, count);
        var explainedEnergy = 0d;
        for (var index = 0; index < count; index++)
        {
            explainedEnergy += coefficients[index] * crossProducts[referenceChannels[index], target];
        }

        var residual = Math.Max(0, targetEnergy - Math.Clamp(explainedEnergy, 0, targetEnergy));
        return Math.Clamp(residual / targetEnergy, 0, 1);
    }

    private static double[] SolveLinearSystem(double[,] augmented, int count)
    {
        for (var column = 0; column < count; column++)
        {
            var pivot = column;
            for (var row = column + 1; row < count; row++)
            {
                if (Math.Abs(augmented[row, column]) > Math.Abs(augmented[pivot, column]))
                {
                    pivot = row;
                }
            }

            if (pivot != column)
            {
                for (var value = column; value <= count; value++)
                {
                    (augmented[column, value], augmented[pivot, value]) =
                        (augmented[pivot, value], augmented[column, value]);
                }
            }

            var divisor = augmented[column, column];
            if (Math.Abs(divisor) <= NumericalFloor)
            {
                continue;
            }

            for (var row = column + 1; row < count; row++)
            {
                var factor = augmented[row, column] / divisor;
                for (var value = column; value <= count; value++)
                {
                    augmented[row, value] -= factor * augmented[column, value];
                }
            }
        }

        var result = new double[count];
        for (var row = count - 1; row >= 0; row--)
        {
            var value = augmented[row, count];
            for (var column = row + 1; column < count; column++)
            {
                value -= augmented[row, column] * result[column];
            }

            var divisor = augmented[row, row];
            result[row] = Math.Abs(divisor) <= NumericalFloor ? 0 : value / divisor;
        }

        return result;
    }

    internal static bool IsSurroundPosition(SpeakerPosition position) => position is
        SpeakerPosition.BackLeft
        or SpeakerPosition.BackRight
        or SpeakerPosition.BackCenter
        or SpeakerPosition.SideLeft
        or SpeakerPosition.SideRight;
}
