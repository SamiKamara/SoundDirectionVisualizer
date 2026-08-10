using SoundDirectionVisualizer.App;
using SoundDirectionVisualizer.App.UI;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;
using System.Drawing;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class DirectionOverlayFormTests
{
    [Fact]
    public void SettingsPresentGameProcessCaptureAsAnOptionalDisabledMode()
    {
        var processCaptureEnabled = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            return Descendants<CheckBox>(form).Single(checkBox =>
                checkBox.Text == "Capture only the detected Steam game's process audio (optional)").Checked;
        });

        Assert.False(processCaptureEnabled);
    }

    [Fact]
    public void SettingsSeparateCompleteAmbientAndLoudMarkerControls()
    {
        var controls = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            var groups = Descendants<GroupBox>(form).ToDictionary(group => group.Text);
            var ambient = groups["Ambient markers"];
            var loud = groups["Loud markers"];
            var ambientSlider = Descendants<TrackBar>(ambient).Single();
            var loudSlider = Descendants<TrackBar>(loud).Single();
            var loudThickness = Descendants<NumericUpDown>(loud)
                .Single(numeric => numeric.DecimalPlaces == 1);

            return (
                AmbientLabels: Descendants<Label>(ambient).Select(label => label.Text).ToArray(),
                AmbientButtonCount: Descendants<Button>(ambient).Count(),
                AmbientSliderRange: (ambientSlider.Minimum, ambientSlider.Maximum),
                LoudLabels: Descendants<Label>(loud).Select(label => label.Text).ToArray(),
                LoudButtonCount: Descendants<Button>(loud).Count(),
                LoudSliderRange: (loudSlider.Minimum, loudSlider.Maximum),
                LoudOutlineToggleCount: Descendants<CheckBox>(loud).Count(),
                LoudThicknessPrecision: (loudThickness.DecimalPlaces, loudThickness.Increment));
        });

        Assert.Contains("Size (% of base)", controls.AmbientLabels);
        Assert.Contains("Opacity", controls.AmbientLabels);
        Assert.Contains("Fill color", controls.AmbientLabels);
        Assert.Equal(1, controls.AmbientButtonCount);
        Assert.Equal((10, 100), controls.AmbientSliderRange);

        Assert.Contains("Size (% of base)", controls.LoudLabels);
        Assert.Contains("Opacity", controls.LoudLabels);
        Assert.Contains("Fill color", controls.LoudLabels);
        Assert.Contains("Outline color", controls.LoudLabels);
        Assert.Contains("Outline thickness (px)", controls.LoudLabels);
        Assert.Equal(2, controls.LoudButtonCount);
        Assert.Equal((10, 100), controls.LoudSliderRange);
        Assert.Equal(1, controls.LoudOutlineToggleCount);
        Assert.Equal((1, 0.1m), controls.LoudThicknessPrecision);
    }

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

    [Fact]
    public void LoudCurrentMarkerIsLargerAndHasTheConfiguredBlackOutline()
    {
        var markers = RunOnStaThread(() =>
        {
            var ambient = RenderCurrentMarker(SoundLoudness.Ambient);
            var loud = RenderCurrentMarker(SoundLoudness.Loud);
            return (ambient, loud);
        });

        Assert.True(markers.loud.VisiblePixels > markers.ambient.VisiblePixels);
        Assert.Equal(0, markers.ambient.BlackPixels);
        Assert.True(markers.loud.BlackPixels > 0);
    }

    [Fact]
    public void LoudDelayedMarkerRetainsTheConfiguredBlackOutline()
    {
        var delayed = RunOnStaThread(() => RenderMarker(
            SoundLoudness.Loud,
            showCurrentMarker: false,
            showTrail: true,
            frameAge: TimeSpan.FromSeconds(1)));

        Assert.True(delayed.VisiblePixels > 0);
        Assert.True(delayed.BlackPixels > 0);
    }

    [Fact]
    public void LoudEmphasisMasterToggleRendersLoudFrameAsAmbient()
    {
        var markers = RunOnStaThread(() =>
        {
            var ambient = RenderCurrentMarker(SoundLoudness.Ambient);
            var disabledLoud = RenderMarker(
                SoundLoudness.Loud,
                showCurrentMarker: true,
                showTrail: false,
                frameAge: TimeSpan.Zero,
                loudEmphasisEnabled: false);
            return (ambient, disabledLoud);
        });

        Assert.Equal(markers.ambient.VisiblePixels, markers.disabledLoud.VisiblePixels);
        Assert.Equal(0, markers.disabledLoud.BlackPixels);
    }

    [Fact]
    public void AmbientAndLoudMarkersUseTheirConfiguredFillColors()
    {
        var markers = RunOnStaThread(() =>
        {
            var ambient = RenderMarker(
                SoundLoudness.Ambient,
                showCurrentMarker: true,
                showTrail: false,
                frameAge: TimeSpan.Zero,
                ambientOpacityPercent: 100,
                ambientColorHex: "#FF0000",
                loudColorHex: "#0000FF",
                loudOutlineEnabled: false);
            var loud = RenderMarker(
                SoundLoudness.Loud,
                showCurrentMarker: true,
                showTrail: false,
                frameAge: TimeSpan.Zero,
                ambientOpacityPercent: 100,
                ambientColorHex: "#FF0000",
                loudColorHex: "#0000FF",
                loudOutlineEnabled: false);
            return (ambient, loud);
        });

        Assert.True(markers.ambient.RedPixels > 0);
        Assert.Equal(0, markers.ambient.BluePixels);
        Assert.True(markers.loud.BluePixels > 0);
        Assert.Equal(0, markers.loud.RedPixels);
    }

    [Fact]
    public void LoudTrailMarkerRendersAboveAnOverlappingAmbientCurrentMarker()
    {
        var pixels = RunOnStaThread(() =>
        {
            using var form = new DirectionOverlayForm();
            form.ApplySettings(new AppSettings
            {
                OverlayOpacityPercent = 100,
                OverlayHeightPercent = 20,
                MarkerSize = 32,
                AmbientMarkerSizePercent = 100,
                AmbientMarkerOpacityPercent = 100,
                AmbientMarkerColorHex = "#FF0000",
                LoudMarkerSizePercent = 100,
                LoudMarkerOpacityPercent = 100,
                LoudMarkerColorHex = "#0000FF",
                LoudMarkerOutlineEnabled = false,
                ShowCurrentDirectionMarkers = true,
                ShowDirectionTrail = true,
                TrailDurationSeconds = 5
            });

            var estimate = new DirectionEstimate(false, 0.5, 0.5, 0, new[] { 0d });
            var now = DateTimeOffset.UtcNow;
            form.UpdateFrame(
                new DirectionFrame(
                    now - TimeSpan.FromMilliseconds(200),
                    new StereoLevels(0.5, 0.5),
                    estimate,
                    SoundLoudness.Loud),
                now);
            form.UpdateFrame(
                new DirectionFrame(
                    now,
                    new StereoLevels(0.5, 0.5),
                    estimate,
                    SoundLoudness.Ambient),
                now);

            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            var redPixels = 0;
            var bluePixels = 0;

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.R > pixel.B)
                    {
                        redPixels++;
                    }
                    else if (pixel.B > pixel.R)
                    {
                        bluePixels++;
                    }
                }
            }

            return (redPixels, bluePixels);
        });

        Assert.True(pixels.redPixels > 0);
        Assert.True(pixels.bluePixels > 0);
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

    private static (int VisiblePixels, int BlackPixels, int RedPixels, int BluePixels) RenderCurrentMarker(
        SoundLoudness loudness) =>
        RenderMarker(
            loudness,
            showCurrentMarker: true,
            showTrail: false,
            frameAge: TimeSpan.Zero);

    private static (int VisiblePixels, int BlackPixels, int RedPixels, int BluePixels) RenderMarker(
        SoundLoudness loudness,
        bool showCurrentMarker,
        bool showTrail,
        TimeSpan frameAge,
        bool loudEmphasisEnabled = true,
        int ambientOpacityPercent = 70,
        string ambientColorHex = "#FFFFFF",
        string loudColorHex = "#FFFFFF",
        bool loudOutlineEnabled = true)
    {
        using var form = new DirectionOverlayForm();
        form.ApplySettings(new AppSettings
        {
            OverlayColorHex = "#FFFFFF",
            OverlayOpacityPercent = 100,
            OverlayHeightPercent = 10,
            MarkerSize = 20,
            AmbientMarkerOpacityPercent = ambientOpacityPercent,
            AmbientMarkerColorHex = ambientColorHex,
            LoudMarkerSizePercent = 150,
            LoudMarkerOpacityPercent = 100,
            LoudMarkerColorHex = loudColorHex,
            LoudMarkerOutlineEnabled = loudOutlineEnabled,
            LoudMarkerOutlineColorHex = "#000000",
            LoudMarkerOutlineThickness = 2,
            LoudSoundEmphasisEnabled = loudEmphasisEnabled,
            ShowCurrentDirectionMarkers = showCurrentMarker,
            ShowDirectionTrail = showTrail,
            TrailDurationSeconds = 5
        });
        var estimate = new DirectionEstimate(false, 0.5, 0.5, 0, new[] { 0d });
        var now = DateTimeOffset.UtcNow;
        form.UpdateFrame(
            new DirectionFrame(now - frameAge, new StereoLevels(0.5, 0.5), estimate, loudness),
            now);

        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        var chromaKey = Color.Magenta.ToArgb();
        var black = Color.Black.ToArgb();
        var red = Color.Red.ToArgb();
        var blue = Color.Blue.ToArgb();
        var visiblePixels = 0;
        var blackPixels = 0;
        var redPixels = 0;
        var bluePixels = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y).ToArgb();
                if (pixel == chromaKey)
                {
                    continue;
                }

                visiblePixels++;
                if (pixel == black)
                {
                    blackPixels++;
                }

                if (pixel == red)
                {
                    redPixels++;
                }

                if (pixel == blue)
                {
                    bluePixels++;
                }
            }
        }

        return (visiblePixels, blackPixels, redPixels, bluePixels);
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

    private static IEnumerable<T> Descendants<T>(Control root)
        where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
