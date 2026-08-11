using NAudio.Wave;
using SoundDirectionVisualizer.App.Services;
using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class ProcessLoopbackFormatSupportTests
{
    [Fact]
    public void Requests71Before51AsFloatWaveFormatExtensible()
    {
        var candidates = ProcessLoopbackFormatSupport.CreateMultichannelCandidates();

        Assert.Equal(["7.1", "5.1"], candidates.Select(candidate => candidate.LayoutName));
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(ProcessLoopbackFormatSupport.SampleRate, candidate.WaveFormat.SampleRate);
            Assert.Equal(ProcessLoopbackFormatSupport.BitsPerSample, candidate.WaveFormat.BitsPerSample);
            Assert.Equal(candidate.Layout.ChannelCount, candidate.WaveFormat.Channels);
            Assert.NotEqual(0, candidate.WaveFormat.ChannelMask);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolvesOnlyRecognizedStandardChannelMasks(bool use71)
    {
        var candidate = ProcessLoopbackFormatSupport.CreateMultichannelCandidates()[use71 ? 0 : 1];

        var success = ProcessLoopbackFormatSupport.TryResolveLayout(
            candidate.WaveFormat,
            out var layout,
            out var reason);

        Assert.True(success, reason);
        Assert.NotNull(layout);
        Assert.Equal(candidate.LayoutName, layout.Name);
    }

    [Fact]
    public void RejectsAnUnknownOrMalformedMaskInsteadOfTrustingChannelCount()
    {
        var unknown = new WaveFormatExtensible(48_000, 32, 6, true, 32, 0x3F);

        var success = ProcessLoopbackFormatSupport.TryResolveLayout(unknown, out var layout, out var reason);

        Assert.False(success);
        Assert.Null(layout);
        Assert.Contains("Unsupported", reason);
    }

    [Fact]
    public void RejectsMultichannelFormatWithoutAnExplicitMask()
    {
        var ordinary = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 6);

        var success = ProcessLoopbackFormatSupport.TryResolveLayout(ordinary, out var layout, out var reason);

        Assert.False(success);
        Assert.Null(layout);
        Assert.Contains("WAVEFORMATEXTENSIBLE", reason);
    }

    [Fact]
    public void StatusExposesPendingVerifiedAndFallbackModesInPlainLanguage()
    {
        var pending = new AudioCaptureStatus(
            "Speakers",
            "device",
            null,
            "stereo",
            AudioEstimatorMode.Stereo,
            MultichannelCaptureState.Probing,
            RequestedLayout: "7.1",
            ObservedLayout: "7.1",
            MultichannelProcessName: "Game");
        var verified = pending with
        {
            SourceName = "Game: Game",
            DeviceId = null,
            ProcessId = 42,
            EstimatorMode = AudioEstimatorMode.Multichannel,
            MultichannelState = MultichannelCaptureState.Verified
        };
        var fallback = pending with { MultichannelState = MultichannelCaptureState.Uninformative };
        var forcedFallback = fallback with
        {
            SourceName = "Game: Game",
            DeviceId = null,
            ProcessId = 42,
            IsMultichannelSourceForced = true
        };

        Assert.Equal("Stereo; checking 7.1 from Game: Game", AudioCaptureStatusFormatter.FormatDetails(pending));
        Assert.Equal("7.1 directional (verified)", AudioCaptureStatusFormatter.FormatDetails(verified));
        Assert.Contains("no independent side/rear content", AudioCaptureStatusFormatter.FormatDetails(fallback));
        Assert.Contains("Forced 7.1 source; stereo estimator", AudioCaptureStatusFormatter.FormatDetails(forcedFallback));
    }
}
