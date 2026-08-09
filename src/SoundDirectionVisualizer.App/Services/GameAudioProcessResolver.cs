using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;

namespace SoundDirectionVisualizer.App.Services;

public sealed record GameAudioProcessTarget(int ProcessId, string ProcessName);

internal sealed record GameAudioProcessCandidate(
    int ProcessId,
    string ProcessName,
    float PeakValue);

public sealed class GameAudioProcessResolver
{
    public GameAudioProcessTarget Resolve(DetectedGameTarget detectedGame)
    {
        ArgumentNullException.ThrowIfNull(detectedGame);

        return Select(
            new GameAudioProcessTarget(detectedGame.ProcessId, detectedGame.ProcessName),
            FindActiveGameAudioProcesses(detectedGame.GameInstallDirectory));
    }

    internal static GameAudioProcessTarget Select(
        GameAudioProcessTarget fallback,
        IReadOnlyList<GameAudioProcessCandidate> activeCandidates)
    {
        var selected = activeCandidates
            .OrderByDescending(candidate => candidate.PeakValue)
            .ThenByDescending(candidate => candidate.ProcessId == fallback.ProcessId)
            .FirstOrDefault();

        return selected is null
            ? fallback
            : new GameAudioProcessTarget(selected.ProcessId, selected.ProcessName);
    }

    private static IReadOnlyList<GameAudioProcessCandidate> FindActiveGameAudioProcesses(
        string gameInstallDirectory)
    {
        var candidates = new Dictionary<int, GameAudioProcessCandidate>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            for (var endpointIndex = 0; endpointIndex < endpoints.Count; endpointIndex++)
            {
                using var endpoint = endpoints[endpointIndex];
                FindActiveGameAudioProcesses(endpoint, gameInstallDirectory, candidates);
            }
        }
        catch
        {
            // Process capture still falls back to the detected game when session enumeration is unavailable.
        }

        return candidates.Values.ToList();
    }

    private static void FindActiveGameAudioProcesses(
        MMDevice endpoint,
        string gameInstallDirectory,
        IDictionary<int, GameAudioProcessCandidate> candidates)
    {
        try
        {
            using var sessionManager = endpoint.AudioSessionManager;
            var sessions = sessionManager.Sessions;

            for (var sessionIndex = 0; sessionIndex < sessions.Count; sessionIndex++)
            {
                try
                {
                    using var session = sessions[sessionIndex];
                    if (session.State != AudioSessionState.AudioSessionStateActive)
                    {
                        continue;
                    }

                    var processId = checked((int)session.GetProcessID);
                    var executablePath = ProcessPathResolver.TryGetExecutablePath(processId);
                    if (executablePath is null
                        || !SteamGamePath.IsWithinInstallDirectory(executablePath, gameInstallDirectory))
                    {
                        continue;
                    }

                    using var process = Process.GetProcessById(processId);
                    using var meter = session.AudioMeterInformation;
                    var candidate = new GameAudioProcessCandidate(
                        processId,
                        process.ProcessName,
                        meter.MasterPeakValue);

                    if (!candidates.TryGetValue(processId, out var existing)
                        || candidate.PeakValue > existing.PeakValue)
                    {
                        candidates[processId] = candidate;
                    }
                }
                catch
                {
                    // A process may exit while its audio session is being inspected.
                }
            }
        }
        catch
        {
            // One endpoint or session must not prevent checking the other active render endpoints.
        }
    }
}
