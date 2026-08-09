using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class SteamGamePathTests
{
    [Fact]
    public void ResolveInstallDirectory_ReturnsFirstDirectoryBelowCommonRoot()
    {
        var root = Path.GetFullPath(Path.Combine("C:\\", "SteamLibrary", "steamapps", "common"));
        var executable = Path.Combine(root, "Example Game", "bin", "win64", "game.exe");

        var result = SteamGamePath.TryResolveInstallDirectory(executable, [root]);

        Assert.Equal(Path.Combine(root, "Example Game"), result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveInstallDirectory_RejectsSiblingWithMatchingPrefix()
    {
        var root = Path.GetFullPath(Path.Combine("C:\\", "SteamLibrary", "steamapps", "common"));
        var executable = Path.Combine(
            Path.GetDirectoryName(root)!,
            $"{Path.GetFileName(root)}-backup",
            "Example Game",
            "game.exe");

        var result = SteamGamePath.TryResolveInstallDirectory(executable, [root]);

        Assert.Null(result);
    }

    [Fact]
    public void WithinInstallDirectory_AcceptsNestedExecutableAndRejectsSiblingGame()
    {
        var install = Path.GetFullPath(Path.Combine("C:\\", "SteamLibrary", "steamapps", "common", "Game"));

        Assert.True(SteamGamePath.IsWithinInstallDirectory(
            Path.Combine(install, "bin", "game.exe"),
            install));
        Assert.False(SteamGamePath.IsWithinInstallDirectory(
            Path.Combine(Path.GetDirectoryName(install)!, "Game Tools", "tool.exe"),
            install));
    }
}
