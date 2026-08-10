using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.App.Services;

internal sealed class CenteredGameAudioFallbackDetector
{
    internal const double CenteredBalanceTolerance = 0.0025;
    internal const int MinimumCenteredFrameCount = 32;
    internal static readonly TimeSpan RequiredCenteredDuration = TimeSpan.FromSeconds(8);
    internal static readonly TimeSpan MaximumQuietGap = TimeSpan.FromSeconds(2);

    private DateTimeOffset? _centeredSince;
    private DateTimeOffset? _lastCenteredFrameAt;
    private int _centeredFrameCount;
    private bool _triggered;

    public bool Observe(DateTimeOffset now, DirectionEstimate estimate)
    {
        if (_triggered)
        {
            return false;
        }

        if (estimate.IsQuiet)
        {
            if (_lastCenteredFrameAt is { } lastCentered
                && now - lastCentered > MaximumQuietGap)
            {
                ResetObservation();
            }

            return false;
        }

        var isCenteredFrontBackPair = estimate.CandidateAzimuths.Count == 2
            && Math.Abs(estimate.Balance) <= CenteredBalanceTolerance;
        if (!isCenteredFrontBackPair)
        {
            ResetObservation();
            return false;
        }

        if (_centeredSince is null
            || _lastCenteredFrameAt is null
            || now < _lastCenteredFrameAt
            || now - _lastCenteredFrameAt > MaximumQuietGap)
        {
            _centeredSince = now;
            _centeredFrameCount = 0;
        }

        _lastCenteredFrameAt = now;
        _centeredFrameCount++;

        if (_centeredFrameCount < MinimumCenteredFrameCount
            || now - _centeredSince < RequiredCenteredDuration)
        {
            return false;
        }

        _triggered = true;
        return true;
    }

    public void Reset()
    {
        _triggered = false;
        ResetObservation();
    }

    private void ResetObservation()
    {
        _centeredSince = null;
        _lastCenteredFrameAt = null;
        _centeredFrameCount = 0;
    }
}
