using System.Diagnostics;
using SoundDirectionVisualizer.App;
using SoundDirectionVisualizer.App.Services;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;

if (args.Length > 0 && args[0].Equals("--resolve-game-audio", StringComparison.OrdinalIgnoreCase))
{
    var detected = new GameWindowMonitor(new SteamLibraryService()).Detect()
        ?? throw new InvalidOperationException("No running Steam game window was detected.");
    if (args.Length > 1 && int.TryParse(args[1], out var requestedDetectedProcessId))
    {
        using var requestedDetectedProcess = Process.GetProcessById(requestedDetectedProcessId);
        detected = detected with
        {
            ProcessId = requestedDetectedProcess.Id,
            ProcessName = requestedDetectedProcess.ProcessName,
            ExecutablePath = ProcessPathResolver.TryGetExecutablePath(requestedDetectedProcess)
                ?? detected.ExecutablePath
        };
    }

    var selected = new GameAudioProcessResolver().Resolve(detected);

    Console.WriteLine($"Detected window process: {detected.ProcessName} ({detected.ProcessId})");
    Console.WriteLine($"Detected executable: {detected.ExecutablePath}");
    Console.WriteLine($"Game install directory: {detected.GameInstallDirectory}");
    Console.WriteLine($"Selected audio process: {selected.ProcessName} ({selected.ProcessId})");
    return;
}

using var targetProcess = args.Length > 0 && int.TryParse(args[0], out var requestedProcessId)
    ? Process.GetProcessById(requestedProcessId)
    : Process.GetProcessesByName("DayZ_x64").SingleOrDefault()
        ?? throw new InvalidOperationException("DayZ_x64 is not running and no process ID was supplied.");
var durationSeconds = args.Length > 1 && int.TryParse(args[1], out var requestedSeconds)
    ? Math.Clamp(requestedSeconds, 1, 300)
    : 15;
var frames = new List<DirectionFrame>();
var gate = new Lock();

using var capture = new AudioCaptureService();
capture.FrameAvailable += (_, frame) =>
{
    lock (gate)
    {
        frames.Add(frame);
    }
};
capture.CaptureFailed += (_, message) => Console.Error.WriteLine($"Capture error: {message}");

await capture.StartAsync(
    new AppSettings(),
    targetProcess.Id,
    targetProcess.ProcessName);

Console.WriteLine($"Requested process: {targetProcess.ProcessName} ({targetProcess.Id})");
Console.WriteLine($"Active source: {capture.ActiveDeviceName}");
Console.WriteLine($"Active process ID: {capture.ActiveProcessId?.ToString() ?? "none (output fallback)"}");
Console.WriteLine($"Format: {capture.FormatDescription}");
PrintCaptureStatus(capture.CurrentStatus);
if (capture.ProcessCaptureFallbackReason is not null)
{
    Console.WriteLine($"Process fallback reason: {capture.ProcessCaptureFallbackReason}");
}

Console.WriteLine($"Capturing {durationSeconds} seconds through the production service...");
await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

DirectionFrame[] snapshot;
lock (gate)
{
    snapshot = [.. frames];
}

var active = snapshot.Where(frame => !frame.Estimate.IsQuiet).ToArray();
var balances = active.Select(frame => frame.Estimate.Balance).Order().ToArray();
var absoluteBalances = balances.Select(Math.Abs).Order().ToArray();
var combinedLevels = active.Select(frame => frame.Levels.CombinedRms).Order().ToArray();
var loudFrameThreshold = Percentile(combinedLevels, 0.90);
var loudFrames = active
    .Where(frame => frame.Levels.CombinedRms >= loudFrameThreshold)
    .ToArray();
var emphasizedFrames = active
    .Where(frame => frame.Loudness == SoundLoudness.Loud)
    .ToArray();
var hardSideFrames = active.Count(frame => frame.Estimate.CandidateAzimuths.Count == 1);
var loudHardSideFrames = loudFrames.Count(frame => frame.Estimate.CandidateAzimuths.Count == 1);

Console.WriteLine($"Frames: {snapshot.Length}, active frames: {active.Length}");
Console.WriteLine(
    $"Signed balance min={Percentile(balances, 0):F6} " +
    $"p10={Percentile(balances, 0.10):F6} " +
    $"p50={Percentile(balances, 0.50):F6} " +
    $"p90={Percentile(balances, 0.90):F6} " +
    $"max={Percentile(balances, 1):F6}");
Console.WriteLine(
    $"Absolute balance p50={Percentile(absoluteBalances, 0.50):F6} " +
    $"p90={Percentile(absoluteBalances, 0.90):F6} " +
    $"p99={Percentile(absoluteBalances, 0.99):F6} " +
    $"max={Percentile(absoluteBalances, 1):F6}");
Console.WriteLine(
    $"Combined level p50={Percentile(combinedLevels, 0.50):F6} " +
    $"p90={Percentile(combinedLevels, 0.90):F6} " +
    $"p99={Percentile(combinedLevels, 0.99):F6} " +
    $"max={Percentile(combinedLevels, 1):F6}");
Console.WriteLine($"Classified loud frames: {emphasizedFrames.Length}/{active.Length}");
Console.WriteLine(
    $"Exact hard-side frames: {hardSideFrames}/{active.Length}; " +
    $"loudest decile: {loudHardSideFrames}/{loudFrames.Length}");
Console.WriteLine("Final capture status:");
PrintCaptureStatus(capture.CurrentStatus);

static void PrintCaptureStatus(AudioCaptureStatus? status)
{
    if (status is null)
    {
        Console.WriteLine("Capture status: unavailable");
        return;
    }

    Console.WriteLine($"Requested layout: {status.RequestedLayout ?? "none"}");
    Console.WriteLine($"Observed layout: {status.ObservedLayout ?? "none"}");
    Console.WriteLine($"Estimator mode: {status.EstimatorMode}");
    Console.WriteLine($"Validation state: {status.MultichannelState}");
    Console.WriteLine($"Fallback reason: {status.FallbackReason ?? "none"}");
}

static double Percentile(double[] sorted, double percentile)
{
    if (sorted.Length == 0)
    {
        return 0;
    }

    var index = Math.Clamp(
        (int)Math.Ceiling(sorted.Length * percentile) - 1,
        0,
        sorted.Length - 1);
    return sorted[index];
}
