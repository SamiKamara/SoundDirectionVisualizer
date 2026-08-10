using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class AudioEndpointServiceTests
{
    [Fact]
    public void SelectsTheStrongestActiveStereoEndpoint()
    {
        var candidates = new[]
        {
            new AudioEndpointActivityCandidate("quiet", "Quiet", 2, 0.0009f, IsDefault: false),
            new AudioEndpointActivityCandidate("surround", "Surround", 6, 0.9f, IsDefault: false),
            new AudioEndpointActivityCandidate("stereo", "Stereo", 2, 0.4f, IsDefault: false),
            new AudioEndpointActivityCandidate("strongest", "Strongest", 2, 0.7f, IsDefault: false)
        };

        var selected = AudioEndpointService.SelectActiveStereoRenderEndpoint(
            candidates,
            excludedDeviceId: null,
            AudioEndpointService.MinimumActivePeak);

        Assert.NotNull(selected);
        Assert.Equal("strongest", selected.Id);
    }

    [Fact]
    public void IgnoresTheCurrentEndpointAndNonStereoOrSilentCandidates()
    {
        var candidates = new[]
        {
            new AudioEndpointActivityCandidate("current", "Current", 2, 0.9f, IsDefault: true),
            new AudioEndpointActivityCandidate("surround", "Surround", 8, 0.8f, IsDefault: false),
            new AudioEndpointActivityCandidate("quiet", "Quiet", 2, 0.0001f, IsDefault: false)
        };

        var selected = AudioEndpointService.SelectActiveStereoRenderEndpoint(
            candidates,
            excludedDeviceId: "CURRENT",
            AudioEndpointService.MinimumActivePeak);

        Assert.Null(selected);
    }

    [Fact]
    public void PrefersTheDefaultEndpointWhenPeakValuesTie()
    {
        var candidates = new[]
        {
            new AudioEndpointActivityCandidate("other", "Other", 2, 0.5f, IsDefault: false),
            new AudioEndpointActivityCandidate("default", "Default", 2, 0.5f, IsDefault: true)
        };

        var selected = AudioEndpointService.SelectActiveStereoRenderEndpoint(
            candidates,
            excludedDeviceId: null,
            AudioEndpointService.MinimumActivePeak);

        Assert.NotNull(selected);
        Assert.Equal("default", selected.Id);
    }
}
