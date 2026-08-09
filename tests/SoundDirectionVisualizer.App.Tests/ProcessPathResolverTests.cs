using SoundDirectionVisualizer.App.Services;
using System.Diagnostics;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class ProcessPathResolverTests
{
    [Fact]
    public void LimitedInformationQuery_ResolvesCurrentProcess()
    {
        using var process = Process.GetCurrentProcess();

        var actual = ProcessPathResolver.TryGetExecutablePath(process);

        Assert.False(string.IsNullOrWhiteSpace(actual));
        Assert.Equal(
            Path.GetFullPath(Environment.ProcessPath!),
            Path.GetFullPath(actual!),
            ignoreCase: true);
    }

    [Fact]
    public void LimitedInformationQuery_ReturnsNullForInvalidProcessId()
    {
        var actual = ProcessPathResolver.TryGetExecutablePath(int.MaxValue);

        Assert.Null(actual);
    }
}
