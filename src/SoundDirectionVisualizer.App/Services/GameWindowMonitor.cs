using SoundDirectionVisualizer.App.Native;
using System.Diagnostics;

namespace SoundDirectionVisualizer.App.Services;

public sealed record DetectedGameTarget(
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    Screen Screen,
    string ExecutablePath,
    string GameInstallDirectory);

public sealed class GameWindowMonitor
{
    private static readonly TimeSpan FullProcessScanInterval = TimeSpan.FromSeconds(3);
    private readonly SteamLibraryService _steamLibraryService;
    private DateTimeOffset _lastFullProcessScan = DateTimeOffset.MinValue;
    private IntPtr _lastDetectedWindowHandle;
    private DetectedGameTarget? _lastDetectedTarget;

    public GameWindowMonitor(SteamLibraryService steamLibraryService)
    {
        _steamLibraryService = steamLibraryService;
    }

    public DetectedGameTarget? Detect()
    {
        var gameRoots = _steamLibraryService.GetGameInstallRoots();
        if (gameRoots.Count == 0)
        {
            ClearCache();
            return null;
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var cachedForeground = TryGetCachedTarget(foregroundWindow);
        if (cachedForeground is not null)
        {
            return cachedForeground;
        }

        var foregroundTarget = TryCreateTargetFromWindow(foregroundWindow, gameRoots);
        if (foregroundTarget is not null)
        {
            Cache(foregroundWindow, foregroundTarget);
            return foregroundTarget;
        }

        var cachedTarget = TryGetCachedTarget(_lastDetectedWindowHandle);
        if (cachedTarget is not null)
        {
            return cachedTarget;
        }

        if (DateTimeOffset.UtcNow - _lastFullProcessScan < FullProcessScanInterval)
        {
            return null;
        }

        _lastFullProcessScan = DateTimeOffset.UtcNow;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var target = TryCreateTargetFromProcess(process, gameRoots);
                if (target is null)
                {
                    continue;
                }

                Cache(process.MainWindowHandle, target);
                return target;
            }
        }

        ClearCache();
        return null;
    }

    private DetectedGameTarget? TryGetCachedTarget(IntPtr windowHandle)
    {
        if (_lastDetectedTarget is null || windowHandle == IntPtr.Zero || windowHandle != _lastDetectedWindowHandle)
        {
            return null;
        }

        if (!IsViableWindow(windowHandle))
        {
            ClearCache();
            return null;
        }

        return _lastDetectedTarget with { Screen = Screen.FromHandle(windowHandle) };
    }

    private static DetectedGameTarget? TryCreateTargetFromWindow(
        IntPtr windowHandle,
        IReadOnlyList<string> gameRoots)
    {
        if (!IsViableWindow(windowHandle))
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return TryCreateTargetFromProcess(process, gameRoots, windowHandle);
        }
        catch
        {
            return null;
        }
    }

    private static DetectedGameTarget? TryCreateTargetFromProcess(
        Process process,
        IReadOnlyList<string> gameRoots,
        IntPtr? explicitWindowHandle = null)
    {
        try
        {
            if (!explicitWindowHandle.HasValue)
            {
                process.Refresh();
            }

            var windowHandle = explicitWindowHandle ?? process.MainWindowHandle;
            if (!IsViableWindow(windowHandle))
            {
                return null;
            }

            var executablePath = ProcessPathResolver.TryGetExecutablePath(process);
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            var normalized = Path.GetFullPath(executablePath);
            var gameInstallDirectory = SteamGamePath.TryResolveInstallDirectory(normalized, gameRoots);
            if (gameInstallDirectory is null)
            {
                return null;
            }

            return new DetectedGameTarget(
                process.Id,
                process.ProcessName,
                process.MainWindowTitle,
                Screen.FromHandle(windowHandle),
                normalized,
                gameInstallDirectory);
        }
        catch
        {
            return null;
        }
    }

    private void Cache(IntPtr windowHandle, DetectedGameTarget target)
    {
        _lastDetectedWindowHandle = windowHandle;
        _lastDetectedTarget = target;
    }

    private void ClearCache()
    {
        _lastDetectedWindowHandle = IntPtr.Zero;
        _lastDetectedTarget = null;
    }

    private static bool IsViableWindow(IntPtr windowHandle) =>
        windowHandle != IntPtr.Zero
        && NativeMethods.IsWindowVisible(windowHandle)
        && !NativeMethods.IsIconic(windowHandle);
}
