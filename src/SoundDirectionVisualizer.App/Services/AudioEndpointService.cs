using NAudio.CoreAudioApi;

namespace SoundDirectionVisualizer.App.Services;

public sealed record AudioEndpointInfo(string? Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal sealed record AudioEndpointActivityCandidate(
    string Id,
    string DisplayName,
    int ChannelCount,
    float PeakValue,
    bool IsDefault);

public static class AudioEndpointService
{
    internal const float MinimumActivePeak = 0.001f;

    public static IReadOnlyList<AudioEndpointInfo> GetRenderEndpoints()
    {
        var result = new List<AudioEndpointInfo>
        {
            new(null, "Default Windows output device")
        };

        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        foreach (var endpoint in endpoints)
        {
            result.Add(new AudioEndpointInfo(endpoint.ID, endpoint.FriendlyName));
        }

        return result;
    }

    public static AudioEndpointInfo? FindActiveStereoRenderEndpoint(string? excludedDeviceId)
    {
        var candidates = new List<AudioEndpointActivityCandidate>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            string? defaultDeviceId = null;

            try
            {
                using var defaultEndpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                defaultDeviceId = defaultEndpoint.ID;
            }
            catch
            {
            }

            using var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (var index = 0; index < endpoints.Count; index++)
            {
                using var endpoint = endpoints[index];
                if (string.Equals(endpoint.ID, excludedDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    using var audioClient = endpoint.CreateAudioClient();
                    using var meter = endpoint.AudioMeterInformation;
                    candidates.Add(new AudioEndpointActivityCandidate(
                        endpoint.ID,
                        endpoint.FriendlyName,
                        audioClient.MixFormat.Channels,
                        meter.MasterPeakValue,
                        string.Equals(endpoint.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase)));
                }
                catch
                {
                    // A disconnected or transitioning endpoint must not abort the idle probe.
                }
            }
        }
        catch
        {
            return null;
        }

        return SelectActiveStereoRenderEndpoint(candidates, excludedDeviceId, MinimumActivePeak);
    }

    internal static AudioEndpointInfo? SelectActiveStereoRenderEndpoint(
        IReadOnlyList<AudioEndpointActivityCandidate> candidates,
        string? excludedDeviceId,
        float minimumPeak)
    {
        var selected = candidates
            .Where(candidate => candidate.ChannelCount == 2)
            .Where(candidate => candidate.PeakValue >= minimumPeak)
            .Where(candidate => !string.Equals(
                candidate.Id,
                excludedDeviceId,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.PeakValue)
            .ThenByDescending(candidate => candidate.IsDefault)
            .FirstOrDefault();

        return selected is null
            ? null
            : new AudioEndpointInfo(selected.Id, selected.DisplayName);
    }
}
