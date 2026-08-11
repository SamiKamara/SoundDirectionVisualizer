using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class DetectedGameAudioCaptureModeTests
{
    [Fact]
    public void DebugForceTakesPriorityWhenGameAudioIsAvailable()
    {
        var settings = new AppSettings
        {
            DebugForceMultichannelSource = true,
            UseBestAvailableMultichannelAudio = true,
            UseDetectedGameProcessAudio = true
        };

        var mode = DetectedGameAudioCaptureModeResolver.Resolve(
            settings,
            directProcessRequested: true,
            hasDetectedGameAudio: true);

        Assert.Equal(DetectedGameAudioCaptureMode.ForcedMultichannelSource, mode);
    }

    [Fact]
    public void DebugForceWaitsOnEndpointUntilGameAudioIsDetected()
    {
        var settings = new AppSettings { DebugForceMultichannelSource = true };

        var mode = DetectedGameAudioCaptureModeResolver.Resolve(
            settings,
            directProcessRequested: false,
            hasDetectedGameAudio: false);

        Assert.Equal(DetectedGameAudioCaptureMode.EndpointOnly, mode);
    }

    [Theory]
    [InlineData(true, true, (int)DetectedGameAudioCaptureMode.DirectProcess)]
    [InlineData(false, true, (int)DetectedGameAudioCaptureMode.BestAvailableProbe)]
    [InlineData(false, false, (int)DetectedGameAudioCaptureMode.EndpointOnly)]
    public void NormalModesRemainUnchanged(
        bool directProcessRequested,
        bool bestAvailable,
        int expectedMode)
    {
        var settings = new AppSettings
        {
            UseBestAvailableMultichannelAudio = bestAvailable
        };

        var mode = DetectedGameAudioCaptureModeResolver.Resolve(
            settings,
            directProcessRequested,
            hasDetectedGameAudio: true);

        Assert.Equal((DetectedGameAudioCaptureMode)expectedMode, mode);
    }
}
