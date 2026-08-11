using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Direction;

public static class MultichannelDirectionEstimator
{
    private const double MinimumConcentration = 0.12;

    public static DirectionEstimate Estimate(
        ChannelLevels levels,
        double silenceRmsThreshold = 0.00125)
    {
        ArgumentNullException.ThrowIfNull(levels);
        silenceRmsThreshold = Math.Clamp(silenceRmsThreshold, 0, 1);

        var stereoFallback = levels.ToStereoFallback();
        if (stereoFallback.CombinedRms < silenceRmsThreshold)
        {
            return DirectionEstimate.Quiet;
        }

        double x = 0;
        double y = 0;
        double directionalEnergy = 0;
        double leftEnergy = 0;
        double rightEnergy = 0;
        var positionEnergies = new List<(double Azimuth, double Energy)>();

        for (var index = 0; index < levels.Layout.ChannelCount; index++)
        {
            var position = levels.Layout.Positions[index];
            if (position == SpeakerPosition.LowFrequency)
            {
                continue;
            }

            var energy = levels.RmsLevels[index] * levels.RmsLevels[index];
            if (energy <= 0)
            {
                continue;
            }

            var azimuth = GetNominalAzimuth(position);
            var radians = azimuth * Math.PI / 180;
            x += energy * Math.Sin(radians);
            y += energy * Math.Cos(radians);
            directionalEnergy += energy;
            positionEnergies.Add((azimuth, energy));

            var side = Math.Sin(radians);
            if (Math.Abs(side) < 1e-9)
            {
                leftEnergy += energy * 0.5;
                rightEnergy += energy * 0.5;
            }
            else if (side > 0)
            {
                rightEnergy += energy;
            }
            else
            {
                leftEnergy += energy;
            }
        }

        if (directionalEnergy <= 0)
        {
            return DirectionEstimate.Quiet;
        }

        var concentration = Math.Sqrt((x * x) + (y * y)) / directionalEnergy;
        IReadOnlyList<double> candidates;
        if (concentration >= MinimumConcentration)
        {
            candidates = [StereoDirectionEstimator.Normalize(Math.Atan2(x, y) * 180 / Math.PI)];
        }
        else
        {
            var maximumEnergy = positionEnergies.Max(item => item.Energy);
            candidates = positionEnergies
                .Where(item => item.Energy >= maximumEnergy * 0.5)
                .Select(item => StereoDirectionEstimator.Normalize(item.Azimuth))
                .Distinct()
                .ToArray();
        }

        var leftShare = leftEnergy / directionalEnergy;
        var rightShare = rightEnergy / directionalEnergy;
        return new DirectionEstimate(
            false,
            leftShare,
            rightShare,
            rightShare - leftShare,
            candidates);
    }

    public static double GetNominalAzimuth(SpeakerPosition position) => position switch
    {
        SpeakerPosition.FrontCenter => 0,
        SpeakerPosition.FrontRightOfCenter => 15,
        SpeakerPosition.FrontRight => 30,
        SpeakerPosition.SideRight => 90,
        SpeakerPosition.BackRight => 150,
        SpeakerPosition.BackCenter => 180,
        SpeakerPosition.BackLeft => 210,
        SpeakerPosition.SideLeft => 270,
        SpeakerPosition.FrontLeft => 330,
        SpeakerPosition.FrontLeftOfCenter => 345,
        SpeakerPosition.LowFrequency => throw new ArgumentException(
            "The low-frequency effects channel has no directional azimuth.",
            nameof(position)),
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };
}
