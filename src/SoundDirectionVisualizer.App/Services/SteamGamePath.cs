namespace SoundDirectionVisualizer.App.Services;

internal static class SteamGamePath
{
    internal static string? TryResolveInstallDirectory(
        string executablePath,
        IReadOnlyList<string> steamGameRoots)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var normalizedExecutable = Path.GetFullPath(executablePath);

        foreach (var root in steamGameRoots)
        {
            var normalizedRoot = Path.GetFullPath(root);
            var relativePath = Path.GetRelativePath(normalizedRoot, normalizedExecutable);
            if (Path.IsPathRooted(relativePath)
                || relativePath.Equals("..", StringComparison.Ordinal)
                || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var firstSeparator = relativePath.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            if (firstSeparator <= 0)
            {
                continue;
            }

            return Path.Combine(normalizedRoot, relativePath[..firstSeparator]);
        }

        return null;
    }

    internal static bool IsWithinInstallDirectory(string executablePath, string gameInstallDirectory)
    {
        if (string.IsNullOrWhiteSpace(executablePath)
            || string.IsNullOrWhiteSpace(gameInstallDirectory))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(gameInstallDirectory),
            Path.GetFullPath(executablePath));
        return !Path.IsPathRooted(relativePath)
            && !relativePath.Equals("..", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
