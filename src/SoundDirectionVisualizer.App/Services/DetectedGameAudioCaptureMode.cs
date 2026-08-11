namespace SoundDirectionVisualizer.App.Services;

internal enum DetectedGameAudioCaptureMode
{
    EndpointOnly,
    BestAvailableProbe,
    ForcedMultichannelSource,
    DirectProcess
}

internal static class DetectedGameAudioCaptureModeResolver
{
    public static DetectedGameAudioCaptureMode Resolve(
        AppSettings settings,
        bool directProcessRequested,
        bool hasDetectedGameAudio)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!hasDetectedGameAudio)
        {
            return DetectedGameAudioCaptureMode.EndpointOnly;
        }

        if (settings.DebugForceMultichannelSource)
        {
            return DetectedGameAudioCaptureMode.ForcedMultichannelSource;
        }

        if (directProcessRequested)
        {
            return DetectedGameAudioCaptureMode.DirectProcess;
        }

        return settings.UseBestAvailableMultichannelAudio
            ? DetectedGameAudioCaptureMode.BestAvailableProbe
            : DetectedGameAudioCaptureMode.EndpointOnly;
    }
}
