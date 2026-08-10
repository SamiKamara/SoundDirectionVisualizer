using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Visualization;

public readonly record struct DirectionMarkerVisual(
    float Size,
    double Intensity,
    bool IsEmphasized);

public static class DirectionMarkerVisualCalculator
{
    public static DirectionMarkerVisual Calculate(
        float markerSize,
        double freshness,
        SoundLoudness loudness,
        int ambientSizePercent,
        int ambientOpacityPercent,
        int loudSizePercent,
        int loudOpacityPercent)
    {
        markerSize = Math.Max(1, markerSize);
        freshness = Math.Clamp(freshness, 0, 1);
        ambientSizePercent = Math.Clamp(ambientSizePercent, 25, 300);
        ambientOpacityPercent = Math.Clamp(ambientOpacityPercent, 10, 100);
        loudSizePercent = Math.Clamp(loudSizePercent, 25, 300);
        loudOpacityPercent = Math.Clamp(loudOpacityPercent, 10, 100);

        var isLoud = loudness == SoundLoudness.Loud;
        var sizePercent = isLoud ? loudSizePercent : ambientSizePercent;
        var size = markerSize
            * (float)(0.20 + 0.30 * freshness)
            * sizePercent / 100f;

        var configuredIntensity = (isLoud ? loudOpacityPercent : ambientOpacityPercent) / 100d;
        var ageIntensity = 0.30 + 0.70 * freshness;
        return new DirectionMarkerVisual(
            Math.Max(1, size),
            Math.Clamp(configuredIntensity * ageIntensity, 0, 1),
            isLoud);
    }
}
