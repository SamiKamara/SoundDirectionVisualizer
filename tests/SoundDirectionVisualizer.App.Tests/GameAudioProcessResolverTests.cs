using SoundDirectionVisualizer.App.Services;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class GameAudioProcessResolverTests
{
    [Fact]
    public void Select_PrefersCandidateWithStrongestActivePeak()
    {
        var fallback = new GameAudioProcessTarget(10, "Launcher");
        GameAudioProcessCandidate[] candidates =
        [
            new(10, "Launcher", 0.01f),
            new(20, "Game", 0.40f)
        ];

        var result = GameAudioProcessResolver.Select(fallback, candidates);

        Assert.Equal(new GameAudioProcessTarget(20, "Game"), result);
    }

    [Fact]
    public void Select_PrefersDetectedProcessWhenPeaksAreEqual()
    {
        var fallback = new GameAudioProcessTarget(10, "Game");
        GameAudioProcessCandidate[] candidates =
        [
            new(20, "Helper", 0f),
            new(10, "Game", 0f)
        ];

        var result = GameAudioProcessResolver.Select(fallback, candidates);

        Assert.Equal(fallback, result);
    }

    [Fact]
    public void Select_UsesDetectedProcessWhenNoAudioSessionMatches()
    {
        var fallback = new GameAudioProcessTarget(10, "Game");

        var result = GameAudioProcessResolver.Select(fallback, []);

        Assert.Equal(fallback, result);
    }
}
