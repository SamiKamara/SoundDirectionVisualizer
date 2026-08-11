using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class CaptureStatusSessionTests
{
    [Fact]
    public void HistoryKeepsOnlyTheNewestSessionEvents()
    {
        var history = new CaptureSessionHistory();
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < CaptureSessionHistory.MaximumEntries + 2; index++)
        {
            history.Add(now.AddSeconds(index), $"Event {index}", $"Reason {index}");
        }

        var events = history.Snapshot();
        Assert.Equal(CaptureSessionHistory.MaximumEntries, events.Count);
        Assert.Equal("Event 2", events[0].Event);
        Assert.Equal($"Event {CaptureSessionHistory.MaximumEntries + 1}", events[^1].Event);
    }

    [Fact]
    public void UninformativeStatusEventIncludesFallbackReasonAndRetryTime()
    {
        var now = DateTimeOffset.Now;
        var nextRetry = now + TimeSpan.FromSeconds(30);
        var status = new AudioCaptureStatus(
            "Headphones",
            "endpoint",
            ProcessId: null,
            "stereo float",
            AudioEstimatorMode.Stereo,
            MultichannelCaptureState.Uninformative,
            RequestedLayout: "7.1",
            ObservedLayout: "7.1",
            MultichannelProcessName: "Game",
            FallbackReason: "Side channels copied the stereo mix.");

        var sessionEvent = CaptureSessionEventFormatter.FromStatus(status, now, nextRetry);

        Assert.Equal("Stereo fallback kept", sessionEvent.Event);
        Assert.Contains("Side channels copied the stereo mix.", sessionEvent.Reason);
        Assert.Contains(nextRetry.ToLocalTime().ToString("HH:mm:ss"), sessionEvent.Reason);
    }

    [Fact]
    public void ForcedUninformativeSourceExplainsThatOnlyTheEstimatorFallsBack()
    {
        var status = new AudioCaptureStatus(
            "Game: Test",
            DeviceId: null,
            ProcessId: 42,
            "7.1 float",
            AudioEstimatorMode.Stereo,
            MultichannelCaptureState.Uninformative,
            RequestedLayout: "7.1",
            ObservedLayout: "7.1",
            MultichannelProcessName: "Test",
            FallbackReason: "No independent side or rear content.",
            IsMultichannelSourceForced: true);

        var sessionEvent = CaptureSessionEventFormatter.FromStatus(
            status,
            DateTimeOffset.Now,
            nextMultichannelRetryAt: null);

        Assert.Equal("Forced multichannel source kept with stereo estimator", sessionEvent.Event);
        Assert.Contains("No independent side or rear content.", sessionEvent.Reason);
    }
}
