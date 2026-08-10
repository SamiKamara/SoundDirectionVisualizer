using SoundDirectionVisualizer.App.Services;
using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class CenteredGameAudioFallbackDetectorTests
{
    private static readonly DirectionEstimate Centered = new(
        IsQuiet: false,
        LeftShare: 0.5,
        RightShare: 0.5,
        Balance: 0,
        CandidateAzimuths: new[] { 0d, 180d });

    private static readonly DirectionEstimate Lateral = new(
        IsQuiet: false,
        LeftShare: 0.25,
        RightShare: 0.75,
        Balance: 0.5,
        CandidateAzimuths: new[] { 75d, 105d });

    [Fact]
    public void SustainedAudibleFrontBackPairTriggersAfterEightSeconds()
    {
        var detector = new CenteredGameAudioFallbackDetector();
        var startedAt = DateTimeOffset.UtcNow;
        var triggered = false;

        for (var index = 0; index <= 80; index++)
        {
            triggered |= detector.Observe(
                startedAt + TimeSpan.FromMilliseconds(index * 100),
                Centered);
        }

        Assert.True(triggered);
        Assert.False(detector.Observe(startedAt + TimeSpan.FromSeconds(9), Centered));
    }

    [Fact]
    public void LateralFrameRestartsTheCenteredObservationPeriod()
    {
        var detector = new CenteredGameAudioFallbackDetector();
        var startedAt = DateTimeOffset.UtcNow;

        ObserveCentered(detector, startedAt, TimeSpan.FromSeconds(4));
        Assert.False(detector.Observe(startedAt + TimeSpan.FromSeconds(4.1), Lateral));
        Assert.False(ObserveCentered(
            detector,
            startedAt + TimeSpan.FromSeconds(4.2),
            TimeSpan.FromSeconds(7.9)));
        Assert.True(detector.Observe(startedAt + TimeSpan.FromSeconds(12.2), Centered));
    }

    [Fact]
    public void LongQuietGapAndSparseFramesCannotTriggerTheFallback()
    {
        var detector = new CenteredGameAudioFallbackDetector();
        var startedAt = DateTimeOffset.UtcNow;

        Assert.False(detector.Observe(startedAt, Centered));
        Assert.False(detector.Observe(
            startedAt + CenteredGameAudioFallbackDetector.MaximumQuietGap + TimeSpan.FromMilliseconds(1),
            DirectionEstimate.Quiet));
        Assert.False(detector.Observe(startedAt + TimeSpan.FromSeconds(3), Centered));
        Assert.False(detector.Observe(startedAt + TimeSpan.FromSeconds(11), Centered));
    }

    [Fact]
    public void ResetAllowsALaterGameSessionToTriggerAgain()
    {
        var detector = new CenteredGameAudioFallbackDetector();
        var startedAt = DateTimeOffset.UtcNow;

        Assert.True(ObserveCentered(
            detector,
            startedAt,
            CenteredGameAudioFallbackDetector.RequiredCenteredDuration));

        detector.Reset();

        Assert.True(ObserveCentered(
            detector,
            startedAt + TimeSpan.FromSeconds(20),
            CenteredGameAudioFallbackDetector.RequiredCenteredDuration));
    }

    private static bool ObserveCentered(
        CenteredGameAudioFallbackDetector detector,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        var triggered = false;
        var frameCount = Math.Max(
            CenteredGameAudioFallbackDetector.MinimumCenteredFrameCount,
            (int)Math.Ceiling(duration.TotalMilliseconds / 100));

        for (var index = 0; index <= frameCount; index++)
        {
            var elapsed = TimeSpan.FromTicks(duration.Ticks * index / frameCount);
            triggered |= detector.Observe(startedAt + elapsed, Centered);
        }

        return triggered;
    }
}
