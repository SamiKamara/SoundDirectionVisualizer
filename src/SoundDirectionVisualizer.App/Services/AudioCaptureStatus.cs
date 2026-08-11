namespace SoundDirectionVisualizer.App.Services;

public enum AudioEstimatorMode
{
    Stereo,
    Multichannel
}

public enum MultichannelCaptureState
{
    NotAttempted,
    Probing,
    Verified,
    Uninformative,
    Unavailable
}

public sealed record AudioCaptureStatus(
    string SourceName,
    string? DeviceId,
    int? ProcessId,
    string FormatDescription,
    AudioEstimatorMode EstimatorMode,
    MultichannelCaptureState MultichannelState,
    string? RequestedLayout = null,
    string? ObservedLayout = null,
    string? MultichannelProcessName = null,
    string? FallbackReason = null,
    bool IsMultichannelSourceForced = false)
{
    public bool IsProcessCapture => ProcessId.HasValue;
}

internal static class AudioCaptureStatusFormatter
{
    public static string FormatDetails(AudioCaptureStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.MultichannelState switch
        {
            MultichannelCaptureState.Probing when status.IsMultichannelSourceForced =>
                $"Forced {status.ObservedLayout ?? status.RequestedLayout} source; validating content with stereo fallback",
            MultichannelCaptureState.Probing when status.IsProcessCapture =>
                $"Stereo fallback; checking {status.ObservedLayout ?? status.RequestedLayout}",
            MultichannelCaptureState.Probing =>
                $"Stereo; checking {status.RequestedLayout} from Game: {status.MultichannelProcessName}",
            MultichannelCaptureState.Verified when status.IsMultichannelSourceForced =>
                $"Forced {status.ObservedLayout} source; directional content verified",
            MultichannelCaptureState.Verified =>
                $"{status.ObservedLayout} directional (verified)",
            MultichannelCaptureState.Uninformative when status.IsMultichannelSourceForced =>
                $"Forced {status.ObservedLayout ?? status.RequestedLayout} source; stereo estimator because side/rear content was uninformative",
            MultichannelCaptureState.Uninformative =>
                $"Stereo fallback; {status.ObservedLayout ?? status.RequestedLayout} had no independent side/rear content",
            MultichannelCaptureState.Unavailable when status.IsMultichannelSourceForced =>
                $"Forced multichannel source unavailable; endpoint stereo fallback ({status.FallbackReason})",
            MultichannelCaptureState.Unavailable when !string.IsNullOrWhiteSpace(status.FallbackReason) =>
                $"Stereo fallback; multichannel unavailable ({status.FallbackReason})",
            _ => "Stereo"
        };
    }
}
