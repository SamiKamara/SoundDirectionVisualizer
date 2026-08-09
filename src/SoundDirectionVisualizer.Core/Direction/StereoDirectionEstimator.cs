using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Direction;

public static class StereoDirectionEstimator
{
    public static DirectionEstimate Estimate(
        StereoLevels levels,
        double silenceRmsThreshold = 0.00125,
        double modelMaximumBalance = 0.60)
    {
        silenceRmsThreshold = Math.Clamp(silenceRmsThreshold, 0, 1);
        modelMaximumBalance = Math.Clamp(modelMaximumBalance, 0.01, 1);

        var total = levels.CombinedRms;
        if (total < silenceRmsThreshold)
        {
            return DirectionEstimate.Quiet;
        }

        var leftShare = levels.LeftRms / total;
        var rightShare = 1 - leftShare;
        var balance = (levels.RightRms - levels.LeftRms) / (total + double.Epsilon);
        var normalized = Math.Clamp(balance / modelMaximumBalance, -1, 1);
        var baseDegrees = Math.Asin(Math.Abs(normalized)) * 180 / Math.PI;

        double first;
        double second;

        if (Math.Abs(normalized) < 1e-9)
        {
            first = 0;
            second = 180;
        }
        else if (normalized > 0)
        {
            first = baseDegrees;
            second = 180 - baseDegrees;
        }
        else
        {
            first = 360 - baseDegrees;
            second = 180 + baseDegrees;
        }

        var candidates = AngularDistance(first, second) < 0.5
            ? new[] { Normalize(first) }
            : new[] { Normalize(first), Normalize(second) };

        return new DirectionEstimate(
            false,
            leftShare,
            rightShare,
            balance,
            candidates);
    }

    public static double Normalize(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double AngularDistance(double first, double second)
    {
        var difference = Math.Abs(Normalize(first) - Normalize(second));
        return Math.Min(difference, 360 - difference);
    }
}
