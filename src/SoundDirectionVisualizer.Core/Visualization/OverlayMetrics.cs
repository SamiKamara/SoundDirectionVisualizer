namespace SoundDirectionVisualizer.Core.Visualization;

public readonly record struct OverlayMetrics(
    float Radius,
    float LineThickness,
    float MarkerSize,
    float ListenerSize,
    float LabelFontSize,
    float LabelDistance,
    float TickLength,
    int Padding)
{
    private const int DesignRadius = 95;
    private const int DesignPadding = 30;

    public static OverlayMetrics Calculate(
        int baseRadius,
        int baseLineThickness,
        int baseMarkerSize,
        int scalePercent)
    {
        var scale = Math.Clamp(scalePercent, 25, 300) / 100f;
        return CalculateWithScale(baseRadius, baseLineThickness, baseMarkerSize, scale);
    }

    public static OverlayMetrics FitToDisplayHeight(
        int baseLineThickness,
        int baseMarkerSize,
        int displayHeight,
        int heightPercent)
    {
        displayHeight = Math.Max(1, displayHeight);
        heightPercent = Math.Clamp(heightPercent, 10, 200);
        var desiredHeight = displayHeight * (heightPercent / 100f);
        var designHeight = 2f * (DesignRadius + DesignPadding);
        var scale = desiredHeight / designHeight;

        return CalculateWithScale(
            DesignRadius,
            baseLineThickness,
            baseMarkerSize,
            scale);
    }

    private static OverlayMetrics CalculateWithScale(
        int baseRadius,
        int baseLineThickness,
        int baseMarkerSize,
        float scale)
    {
        return new OverlayMetrics(
            Math.Max(10, baseRadius * scale),
            Math.Max(1, baseLineThickness * scale),
            Math.Max(2, baseMarkerSize * scale),
            Math.Max(2, Math.Max(4, baseMarkerSize / 2f) * scale),
            Math.Max(5, 9 * scale),
            Math.Max(10, (baseRadius + 13) * scale),
            Math.Max(2, 7 * scale),
            Math.Max(8, (int)Math.Ceiling(DesignPadding * scale)));
    }
}
