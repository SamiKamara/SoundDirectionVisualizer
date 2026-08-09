using SoundDirectionVisualizer.Core.Visualization;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class OverlayMetricsTests
{
    [Fact]
    public void OneHundredPercentPreservesBaseDimensions()
    {
        var metrics = OverlayMetrics.Calculate(95, 3, 12, 100);

        Assert.Equal(95f, metrics.Radius);
        Assert.Equal(3f, metrics.LineThickness);
        Assert.Equal(12f, metrics.MarkerSize);
        Assert.Equal(6f, metrics.ListenerSize);
        Assert.Equal(9f, metrics.LabelFontSize);
        Assert.Equal(108f, metrics.LabelDistance);
        Assert.Equal(7f, metrics.TickLength);
        Assert.Equal(30, metrics.Padding);
    }

    [Fact]
    public void ScaleChangesEveryVisualDimensionTogether()
    {
        var metrics = OverlayMetrics.Calculate(80, 4, 10, 150);

        Assert.Equal(120f, metrics.Radius);
        Assert.Equal(6f, metrics.LineThickness);
        Assert.Equal(15f, metrics.MarkerSize);
        Assert.Equal(7.5f, metrics.ListenerSize);
        Assert.Equal(13.5f, metrics.LabelFontSize);
        Assert.Equal(139.5f, metrics.LabelDistance);
        Assert.Equal(10.5f, metrics.TickLength);
        Assert.Equal(45, metrics.Padding);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(500, 300)]
    public void ScaleIsClampedToSupportedRange(int requestedScale, int expectedScale)
    {
        var actual = OverlayMetrics.Calculate(100, 4, 12, requestedScale);
        var expected = OverlayMetrics.Calculate(100, 4, 12, expectedScale);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DisplayRelativeSizeMatchesRequestedHeightPercentage()
    {
        var metrics = OverlayMetrics.FitToDisplayHeight(3, 12, 1000, 80);
        var overlayHeight = 2 * (metrics.Radius + metrics.Padding);

        Assert.Equal(800, overlayHeight, precision: 3);
    }
}
