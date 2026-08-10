namespace SoundDirectionVisualizer.App.Services;

internal sealed class SilentEndpointProbeSchedule
{
    internal static readonly TimeSpan SilenceGracePeriod = TimeSpan.FromSeconds(8);
    internal static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(30);

    private DateTimeOffset _nextProbeAt = DateTimeOffset.MaxValue;
    private TimeSpan _retryDelay = InitialRetryDelay;

    public void Reset(DateTimeOffset now)
    {
        _retryDelay = InitialRetryDelay;
        _nextProbeAt = now + SilenceGracePeriod;
    }

    public void ObserveAudibleFrame(DateTimeOffset now) => Reset(now);

    public bool TryBeginProbe(DateTimeOffset now)
    {
        if (now < _nextProbeAt)
        {
            return false;
        }

        _nextProbeAt = DateTimeOffset.MaxValue;
        return true;
    }

    public void CompleteProbe(DateTimeOffset now, bool sourceChanged)
    {
        if (sourceChanged)
        {
            Reset(now);
            return;
        }

        _nextProbeAt = now + _retryDelay;
        _retryDelay = TimeSpan.FromTicks(Math.Min(
            _retryDelay.Ticks * 2,
            MaximumRetryDelay.Ticks));
    }
}
