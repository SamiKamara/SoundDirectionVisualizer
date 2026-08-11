namespace SoundDirectionVisualizer.App.Services;

internal sealed record CaptureSessionEvent(
    DateTimeOffset Timestamp,
    string Event,
    string Reason);

internal sealed record AudioStatusSnapshot(
    AudioCaptureStatus? CurrentStatus,
    DateTimeOffset? NextMultichannelRetryAt,
    IReadOnlyList<CaptureSessionEvent> Events,
    bool DebugForceMultichannelSourceEnabled = false)
{
    public static AudioStatusSnapshot Empty { get; } = new(null, null, []);
}

internal sealed class CaptureSessionHistory
{
    internal const int MaximumEntries = 100;

    private readonly object _gate = new();
    private readonly Queue<CaptureSessionEvent> _events = new();

    public void Add(DateTimeOffset timestamp, string eventName, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_gate)
        {
            _events.Enqueue(new CaptureSessionEvent(timestamp, eventName, reason));
            while (_events.Count > MaximumEntries)
            {
                _events.Dequeue();
            }
        }
    }

    public IReadOnlyList<CaptureSessionEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }
}

internal static class CaptureSessionEventFormatter
{
    public static CaptureSessionEvent FromStatus(
        AudioCaptureStatus status,
        DateTimeOffset timestamp,
        DateTimeOffset? nextMultichannelRetryAt)
    {
        ArgumentNullException.ThrowIfNull(status);

        var requestedLayout = status.RequestedLayout ?? "standard 7.1/5.1";
        var observedLayout = status.ObservedLayout ?? "no recognized layout";
        var processName = status.MultichannelProcessName ?? "the detected game audio process";

        return status.MultichannelState switch
        {
            MultichannelCaptureState.Probing when status.IsMultichannelSourceForced => new CaptureSessionEvent(
                timestamp,
                "Debug-forced multichannel source activated",
                $"{observedLayout} from {processName} was available, so process loopback became the active source immediately. " +
                "Content validation still controls whether the multichannel or stereo-fold-down estimator is used."),
            MultichannelCaptureState.Probing => new CaptureSessionEvent(
                timestamp,
                "Multichannel validation started",
                $"Checking {requestedLayout} from {processName}; observed {observedLayout}. " +
                "The working stereo source remains active until independent side or rear content is verified."),
            MultichannelCaptureState.Verified when status.IsMultichannelSourceForced => new CaptureSessionEvent(
                timestamp,
                "Forced source verified for multichannel direction",
                $"{observedLayout} from {processName} contained independent side or rear content, so the forced source now uses the multichannel estimator."),
            MultichannelCaptureState.Verified => new CaptureSessionEvent(
                timestamp,
                "Verified multichannel audio activated",
                $"{observedLayout} from {processName} contained independent side or rear content, so the directional estimator can use every mapped channel."),
            MultichannelCaptureState.Uninformative when status.IsMultichannelSourceForced => new CaptureSessionEvent(
                timestamp,
                "Forced multichannel source kept with stereo estimator",
                status.FallbackReason ?? $"{observedLayout} remained the forced source, but its uninformative side/rear content cannot support a multichannel direction estimate."),
            MultichannelCaptureState.Uninformative => new CaptureSessionEvent(
                timestamp,
                "Stereo fallback kept",
                AppendRetry(status.FallbackReason ?? $"{observedLayout} did not contain useful independent side or rear content.", nextMultichannelRetryAt)),
            MultichannelCaptureState.Unavailable when status.IsMultichannelSourceForced => new CaptureSessionEvent(
                timestamp,
                "Debug-forced multichannel source unavailable",
                AppendRetry(status.FallbackReason ?? $"Windows did not expose a supported {requestedLayout} stream.", nextMultichannelRetryAt)),
            MultichannelCaptureState.Unavailable => new CaptureSessionEvent(
                timestamp,
                "Multichannel capture unavailable",
                AppendRetry(status.FallbackReason ?? $"Windows did not expose a supported {requestedLayout} stream.", nextMultichannelRetryAt)),
            _ => new CaptureSessionEvent(
                timestamp,
                "Stereo capture active",
                $"Using {status.SourceName} in {status.FormatDescription}. Stereo preserves left/right direction but keeps front/back ambiguity explicit.")
        };
    }

    private static string AppendRetry(string reason, DateTimeOffset? nextRetryAt) =>
        nextRetryAt is null
            ? reason
            : $"{reason} Next automatic validation attempt: {nextRetryAt.Value.ToLocalTime():HH:mm:ss}.";
}
