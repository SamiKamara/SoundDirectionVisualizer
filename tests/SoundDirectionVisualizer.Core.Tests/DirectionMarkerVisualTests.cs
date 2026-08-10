using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Visualization;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class DirectionMarkerVisualTests
{
    [Fact]
    public void AmbientSpawnMatchesFreshTrailPointSize()
    {
        var spawn = Calculate(SoundLoudness.Ambient, freshness: 1);
        var freshTrail = Calculate(SoundLoudness.Ambient, freshness: 1);

        Assert.Equal(freshTrail.Size, spawn.Size);
        Assert.Equal(10, spawn.Size, precision: 3);
    }

    [Fact]
    public void LoudMarkerUsesConfiguredSizeAndOpacityEmphasis()
    {
        var ambient = Calculate(SoundLoudness.Ambient, freshness: 1);
        var loud = Calculate(SoundLoudness.Loud, freshness: 1);

        Assert.Equal(ambient.Size * 1.5f, loud.Size, precision: 3);
        Assert.Equal(0.70, ambient.Intensity, precision: 3);
        Assert.Equal(1.00, loud.Intensity, precision: 3);
        Assert.False(ambient.IsEmphasized);
        Assert.True(loud.IsEmphasized);
    }

    [Fact]
    public void AmbientAndLoudMarkerSizesAreConfiguredIndependently()
    {
        var ambient = Calculate(
            SoundLoudness.Ambient,
            freshness: 1,
            ambientSizePercent: 75,
            loudSizePercent: 200);
        var loud = Calculate(
            SoundLoudness.Loud,
            freshness: 1,
            ambientSizePercent: 75,
            loudSizePercent: 200);

        Assert.Equal(7.5f, ambient.Size, precision: 3);
        Assert.Equal(20f, loud.Size, precision: 3);
    }

    [Fact]
    public void DelayedMarkerShrinksAndFades()
    {
        var fresh = Calculate(SoundLoudness.Loud, freshness: 1);
        var delayed = Calculate(SoundLoudness.Loud, freshness: 0.25);

        Assert.True(delayed.Size < fresh.Size);
        Assert.True(delayed.Intensity < fresh.Intensity);
    }

    private static DirectionMarkerVisual Calculate(
        SoundLoudness loudness,
        double freshness,
        int ambientSizePercent = 100,
        int loudSizePercent = 150) =>
        DirectionMarkerVisualCalculator.Calculate(
            markerSize: 20,
            freshness,
            loudness,
            ambientSizePercent,
            ambientOpacityPercent: 70,
            loudSizePercent,
            loudOpacityPercent: 100);
}
