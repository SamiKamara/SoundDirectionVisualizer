using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace SoundDirectionVisualizer.App.Services;

public sealed class SteamLibraryService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly Regex VdfPathRegex = new(
        "\"path\"\\s+\"(?<path>.+?)\"",
        RegexOptions.Compiled);
    private static readonly Regex LegacyLibraryRegex = new(
        "^\\s*\"\\d+\"\\s+\"(?<path>.+?)\"\\s*$",
        RegexOptions.Compiled);

    private readonly object _syncRoot = new();
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private IReadOnlyList<string>? _cachedLibraryRoots;

    public IReadOnlyList<string> GetGameInstallRoots()
    {
        return GetLibraryRoots()
            .Select(root => Path.Combine(root, "steamapps", "common"))
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetLibraryRoots()
    {
        lock (_syncRoot)
        {
            if (_cachedLibraryRoots is not null && DateTimeOffset.UtcNow - _lastRefresh < CacheLifetime)
            {
                return _cachedLibraryRoots;
            }

            _cachedLibraryRoots = DiscoverLibraryRoots();
            _lastRefresh = DateTimeOffset.UtcNow;
            return _cachedLibraryRoots;
        }
    }

    private static IReadOnlyList<string> DiscoverLibraryRoots()
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateInstallCandidates())
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var normalized = NormalizePath(candidate);
            results.Add(normalized);

            var libraryFile = Path.Combine(normalized, "steamapps", "libraryfolders.vdf");
            foreach (var library in ParseLibraryFolders(libraryFile))
            {
                if (Directory.Exists(library))
                {
                    results.Add(NormalizePath(library));
                }
            }
        }

        return results.ToList();
    }

    private static IEnumerable<string> EnumerateInstallCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in GetRegistryCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate);
            }
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Steam"));

        return candidates.Select(NormalizePath);
    }

    private static IEnumerable<string> GetRegistryCandidates()
    {
        using var currentUser = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        yield return currentUser?.GetValue("SteamPath") as string ?? string.Empty;
        yield return currentUser?.GetValue("SteamExe") is string steamExecutable
            ? Path.GetDirectoryName(steamExecutable) ?? string.Empty
            : string.Empty;

        using var localMachine32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
            .OpenSubKey(@"SOFTWARE\Valve\Steam");
        yield return localMachine32?.GetValue("InstallPath") as string ?? string.Empty;

        using var localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        yield return localMachine64?.GetValue("InstallPath") as string ?? string.Empty;
    }

    private static IEnumerable<string> ParseLibraryFolders(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path))
        {
            var pathMatch = VdfPathRegex.Match(line);
            if (pathMatch.Success)
            {
                yield return NormalizePath(pathMatch.Groups["path"].Value);
                continue;
            }

            var legacyMatch = LegacyLibraryRegex.Match(line);
            if (legacyMatch.Success)
            {
                yield return NormalizePath(legacyMatch.Groups["path"].Value);
            }
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.Replace('/', '\\').Replace(@"\\", @"\"));
}
