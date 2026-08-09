using NAudio.CoreAudioApi;

namespace SoundDirectionVisualizer.App.Services;

public sealed record AudioEndpointInfo(string? Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public static class AudioEndpointService
{
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
}
