namespace SoundDirectionVisualizer.Core.Direction;

public sealed record DirectionEstimate(
    bool IsQuiet,
    double LeftShare,
    double RightShare,
    double Balance,
    IReadOnlyList<double> CandidateAzimuths)
{
    public static DirectionEstimate Quiet { get; } = new(
        true,
        0.5,
        0.5,
        0,
        Array.Empty<double>());
}
