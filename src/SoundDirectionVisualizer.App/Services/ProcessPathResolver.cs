using SoundDirectionVisualizer.App.Native;
using System.Diagnostics;
using System.Text;

namespace SoundDirectionVisualizer.App.Services;

public static class ProcessPathResolver
{
    private const int MaximumWindowsPathLength = 32_768;

    public static string? TryGetExecutablePath(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        var limitedQueryPath = TryGetExecutablePath(process.Id);
        if (!string.IsNullOrWhiteSpace(limitedQueryPath))
        {
            return limitedQueryPath;
        }

        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public static string? TryGetExecutablePath(int processId)
    {
        if (processId <= 0 || !OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var processHandle = NativeMethods.OpenProcess(
                NativeMethods.ProcessQueryLimitedInformation,
                inheritHandle: false,
                (uint)processId);
            if (processHandle.IsInvalid)
            {
                return null;
            }

            var path = new StringBuilder(MaximumWindowsPathLength);
            var pathLength = (uint)path.Capacity;
            return NativeMethods.QueryFullProcessImageName(
                processHandle,
                flags: 0,
                path,
                ref pathLength)
                ? path.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
