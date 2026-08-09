namespace SoundDirectionVisualizer.Core.Direction;

public sealed record DirectionTrailPoint(double Azimuth, DateTimeOffset Timestamp);

public sealed class DirectionTrail
{
    private readonly List<DirectionTrailPoint> _points = new();

    public IReadOnlyList<DirectionTrailPoint> Points => _points;

    public void Add(DirectionEstimate estimate, DateTimeOffset timestamp)
    {
        if (estimate.IsQuiet)
        {
            return;
        }

        foreach (var azimuth in estimate.CandidateAzimuths)
        {
            _points.Add(new DirectionTrailPoint(StereoDirectionEstimator.Normalize(azimuth), timestamp));
        }
    }

    public void Prune(DateTimeOffset now, TimeSpan maximumAge)
    {
        _points.RemoveAll(point => now - point.Timestamp > maximumAge);
    }

    public void Clear() => _points.Clear();
}
