using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class DirectionTrailTests
{
    [Fact]
    public void AddsCandidatesAndPrunesExpiredPoints()
    {
        var trail = new DirectionTrail();
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        var estimate = new DirectionEstimate(false, 0.4, 0.6, 0.2, new[] { 20d, 160d });

        trail.Add(estimate, now - TimeSpan.FromSeconds(6));
        trail.Add(estimate, now - TimeSpan.FromSeconds(1));
        trail.Prune(now, TimeSpan.FromSeconds(5));

        Assert.Equal(2, trail.Points.Count);
        Assert.All(trail.Points, point => Assert.Equal(now - TimeSpan.FromSeconds(1), point.Timestamp));
    }
}
