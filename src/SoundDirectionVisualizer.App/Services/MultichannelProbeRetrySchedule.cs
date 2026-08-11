namespace SoundDirectionVisualizer.App.Services;

internal sealed class MultichannelProbeRetrySchedule
{
    internal static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan DeferredRetryDelay = TimeSpan.FromSeconds(5);

    private DateTimeOffset? _nextRetryAt;
    private TimeSpan _retryDelay = InitialRetryDelay;

    public DateTimeOffset? NextRetryAt => _nextRetryAt;

    public void Reset()
    {
        _nextRetryAt = null;
        _retryDelay = InitialRetryDelay;
    }

    public void ObserveStatus(AudioCaptureStatus status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);

        switch (status.MultichannelState)
        {
            case MultichannelCaptureState.Verified:
            case MultichannelCaptureState.NotAttempted:
                Reset();
                break;
            case MultichannelCaptureState.Probing:
                _nextRetryAt = null;
                break;
            case MultichannelCaptureState.Uninformative:
            case MultichannelCaptureState.Unavailable:
                if (!status.IsProcessCapture && _nextRetryAt is null)
                {
                    _nextRetryAt = now + _retryDelay;
                    _retryDelay = TimeSpan.FromTicks(Math.Min(
                        _retryDelay.Ticks * 2,
                        MaximumRetryDelay.Ticks));
                }

                break;
        }
    }

    public bool TryBeginRetry(DateTimeOffset now)
    {
        if (_nextRetryAt is null || now < _nextRetryAt.Value)
        {
            return false;
        }

        _nextRetryAt = null;
        return true;
    }

    public void DeferRetry(DateTimeOffset now)
    {
        if (_nextRetryAt is null)
        {
            _nextRetryAt = now + DeferredRetryDelay;
        }
    }
}
