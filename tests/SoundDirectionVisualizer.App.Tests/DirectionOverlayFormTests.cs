using SoundDirectionVisualizer.App;
using SoundDirectionVisualizer.App.UI;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;
using System.Drawing;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class DirectionOverlayFormTests
{
    [Fact]
    public void AppliesSelectedColorWithoutChromaKeyBlendingAndUsesWindowOpacity()
    {
        var result = RunOnStaThread(() =>
        {
            using var form = new DirectionOverlayForm();
            form.ApplySettings(new AppSettings
            {
                OverlayColorHex = "#FFFF00",
                OverlayOpacityPercent = 47,
                OverlayHeightPercent = 10,
                RingThickness = 3,
                MarkerSize = 10,
                ShowCompassRing = true,
                ShowCompassLabels = false,
                ShowDirectionTrail = false
            });

            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));

            var selectedColor = Color.FromArgb(255, 255, 255, 0).ToArgb();
            var chromaKey = Color.Magenta.ToArgb();
            var selectedColorPixels = 0;
            var unexpectedColorPixels = 0;

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y).ToArgb();
                    if (pixel == selectedColor)
                    {
                        selectedColorPixels++;
                    }
                    else if (pixel != chromaKey)
                    {
                        unexpectedColorPixels++;
                    }
                }
            }

            return (form.Opacity, selectedColorPixels, unexpectedColorPixels);
        });

        Assert.Equal(0.47, result.Opacity, precision: 6);
        Assert.True(result.selectedColorPixels > 100);
        Assert.Equal(0, result.unexpectedColorPixels);
    }

    [Fact]
    public void ScaleChangesTheCompleteOverlayWindowSize()
    {
        var sizes = RunOnStaThread(() =>
        {
            using var form = new DirectionOverlayForm();
            var settings = new AppSettings
            {
                OverlayHeightPercent = 10,
                RingThickness = 4,
                MarkerSize = 10
            };

            form.ApplySettings(settings);
            var normal = form.Size;

            settings.OverlayHeightPercent = 20;
            form.ApplySettings(settings);
            return (normal, scaled: form.Size);
        });

        Assert.True(sizes.scaled.Width > sizes.normal.Width * 1.9);
        Assert.True(sizes.scaled.Height > sizes.normal.Height * 1.9);
    }

    [Fact]
    public void EveryOverlayElementCanBeShownIndependently()
    {
        var visiblePixelCounts = RunOnStaThread(() =>
        {
            var cases = new (string Name, Action<AppSettings> Enable, bool NeedsFrame)[]
            {
                ("ring", settings => settings.ShowCompassRing = true, false),
                ("ticks", settings => settings.ShowCardinalTicks = true, false),
                ("rays", settings => settings.ShowCurrentDirectionRays = true, true),
                ("markers", settings => settings.ShowCurrentDirectionMarkers = true, true),
                ("listener", settings => settings.ShowListenerDot = true, false),
                ("trail", settings => settings.ShowDirectionTrail = true, true),
                ("labels", settings => settings.ShowCompassLabels = true, false)
            };
            var results = new Dictionary<string, int>();

            foreach (var testCase in cases)
            {
                var settings = CreateAllElementsHiddenSettings();
                testCase.Enable(settings);
                results[testCase.Name] = CountVisiblePixels(settings, testCase.NeedsFrame);
            }

            return results;
        });

        Assert.All(visiblePixelCounts, result => Assert.True(
            result.Value > 0,
            $"Overlay element '{result.Key}' did not render any pixels."));
    }

    [Fact]
    public void HidingEveryElementProducesAnEmptyOverlay()
    {
        var visiblePixels = RunOnStaThread(() =>
            CountVisiblePixels(CreateAllElementsHiddenSettings(), withDirectionFrame: true));

        Assert.Equal(0, visiblePixels);
    }

    private static AppSettings CreateAllElementsHiddenSettings() => new()
    {
        OverlayColorHex = "#FFFF00",
        OverlayOpacityPercent = 100,
        OverlayHeightPercent = 10,
        // Avoid a one-pixel trail ellipse whose rasterization varies with runner display metrics.
        MarkerSize = 32,
        ShowCompassRing = false,
        ShowCardinalTicks = false,
        ShowCurrentDirectionRays = false,
        ShowCurrentDirectionMarkers = false,
        ShowListenerDot = false,
        ShowDirectionTrail = false,
        ShowCompassLabels = false
    };

    private static int CountVisiblePixels(AppSettings settings, bool withDirectionFrame)
    {
        using var form = new DirectionOverlayForm();
        form.ApplySettings(settings);

        if (withDirectionFrame)
        {
            var estimate = new DirectionEstimate(false, 0.2, 0.8, 0.6, new[] { 90d });
            form.UpdateFrame(
                new DirectionFrame(DateTimeOffset.UtcNow, new StereoLevels(0.2, 0.8), estimate),
                DateTimeOffset.UtcNow);
        }

        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        var chromaKey = Color.Magenta.ToArgb();
        var visiblePixels = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() != chromaKey)
                {
                    visiblePixels++;
                }
            }
        }

        return visiblePixels;
    }

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new AggregateException(failure);
        }

        return result!;
    }
}
