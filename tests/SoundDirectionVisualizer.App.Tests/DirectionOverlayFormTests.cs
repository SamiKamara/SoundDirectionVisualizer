using SoundDirectionVisualizer.App;
using SoundDirectionVisualizer.App.Services;
using SoundDirectionVisualizer.App.UI;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;
using System.Drawing;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class DirectionOverlayFormTests
{
    [Fact]
    public void SettingsRemoveTheDecorativePreviewBadgeAndExposeLiveStatusDetails()
    {
        var now = DateTimeOffset.Now;
        var status = new AudioCaptureStatus(
            "Headphones",
            "endpoint-id",
            ProcessId: null,
            "48000Hz 32-bit IEEE float stereo",
            AudioEstimatorMode.Stereo,
            MultichannelCaptureState.Uninformative,
            RequestedLayout: "7.1",
            ObservedLayout: "7.1",
            MultichannelProcessName: "Game",
            FallbackReason: "The surround channels repeated the stereo mix.");
        var snapshot = new AudioStatusSnapshot(
            status,
            now + TimeSpan.FromSeconds(30),
            [new CaptureSessionEvent(now, "Stereo fallback kept", "The surround channels repeated the stereo mix.")]);

        var view = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings(), snapshot);
            form.Show();
            Application.DoEvents();
            var tabs = Descendants<DarkTabControl>(form).Single();
            var statusPage = tabs.TabPages.Cast<TabPage>().Single(page => page.Text == "Status");
            return (
                TabNames: tabs.TabPages.Cast<TabPage>().Select(page => page.Text).ToArray(),
                AllTabsFit: Enumerable.Range(0, tabs.TabCount).All(index => tabs.GetTabRect(index).Right <= tabs.ClientSize.Width),
                HasPreviewBadge: Descendants<Label>(form).Any(label => label.Text == "LIVE OVERLAY PREVIEW"),
                StatusText: string.Join("\n", Descendants<Label>(statusPage).Select(label => label.Text)),
                EventLog: Descendants<TextBox>(statusPage).Single(textBox => textBox.AccessibleName == "Session event log").Text);
        });

        Assert.Equal(["Audio", "Overlay", "Target display", "Status", "Hotkeys"], view.TabNames);
        Assert.True(view.AllTabsFit);
        Assert.False(view.HasPreviewBadge);
        Assert.Contains("WASAPI endpoint loopback", view.StatusText);
        Assert.Contains("Rejected as uninformative", view.StatusText);
        Assert.Contains("The surround channels repeated the stereo mix.", view.StatusText);
        Assert.Contains("Stereo fallback kept", view.EventLog);
        Assert.Contains("Reason: The surround channels repeated the stereo mix.", view.EventLog);
    }

    [Fact]
    public void SettingsPresentGameProcessCaptureAsAnOptionalDisabledMode()
    {
        var captureModes = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            var checkBoxes = Descendants<CheckBox>(form).ToDictionary(checkBox => checkBox.Text);
            return (
                BestAvailable: checkBoxes["Automatically use verified multichannel game audio when available"].Checked,
                DebugForce: checkBoxes["Debug: force multichannel source when available"].Checked,
                Manual: checkBoxes["Capture only the detected Steam game's process audio (optional)"].Checked,
                Automatic: checkBoxes["Automatically try game-process audio when a running game's output stays centered"].Checked);
        });

        Assert.True(captureModes.BestAvailable);
        Assert.False(captureModes.DebugForce);
        Assert.False(captureModes.Manual);
        Assert.True(captureModes.Automatic);
    }

    [Fact]
    public void DebugForceMultichannelSettingIsSavedFromTheAudioTab()
    {
        var saved = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            form.Show();
            Application.DoEvents();
            var debugForce = Descendants<CheckBox>(form).Single(checkBox =>
                checkBox.Text == "Debug: force multichannel source when available");
            debugForce.Checked = true;
            Descendants<Button>(form).Single(button => button.Text == "Save").PerformClick();
            return form.ResultSettings.DebugForceMultichannelSource;
        });

        Assert.True(saved);
    }

    [Fact]
    public void StatusExplainsForcedSourceSeparatelyFromItsStereoEstimator()
    {
        var status = new AudioCaptureStatus(
            "Game: Test",
            DeviceId: null,
            ProcessId: 42,
            "7.1 float",
            AudioEstimatorMode.Stereo,
            MultichannelCaptureState.Uninformative,
            RequestedLayout: "7.1",
            ObservedLayout: "7.1",
            MultichannelProcessName: "Test",
            FallbackReason: "No independent side or rear content.",
            IsMultichannelSourceForced: true);
        var snapshot = new AudioStatusSnapshot(
            status,
            NextMultichannelRetryAt: null,
            Events: [],
            DebugForceMultichannelSourceEnabled: true);

        var statusText = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings(), snapshot);
            var tabs = Descendants<DarkTabControl>(form).Single();
            var statusPage = tabs.TabPages.Cast<TabPage>().Single(page => page.Text == "Status");
            return string.Join("\n", Descendants<Label>(statusPage).Select(label => label.Text));
        });

        Assert.Contains("Debug force enabled", statusText);
        Assert.Contains("Debug-forced multichannel process loopback", statusText);
        Assert.Contains("Stereo left/right", statusText);
        Assert.Contains("Forced source active; using stereo fold-down", statusText);
    }

    [Fact]
    public void ForcedDebugStatusRendersEveryMonitoredChannelLive()
    {
        var snapshot = new AudioStatusSnapshot(
            CurrentStatus: null,
            NextMultichannelRetryAt: null,
            Events: [],
            DebugForceMultichannelSourceEnabled: true);
        var frame = AudioChannelMeterFrameFactory.FromMultichannel(
            DateTimeOffset.UtcNow,
            "Game: Test",
            new ChannelLevels(
                ChannelLayout.Surround71,
                [1, 0.5, 0.25, 0.125, 0.0625, 0.03125, 0.015625, 0.0078125]));

        var view = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings(), snapshot);
            form.Show();
            var tabs = Descendants<DarkTabControl>(form).Single();
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(page => page.Text == "Status");
            form.UpdateChannelVisualization(frame);
            Application.DoEvents();
            var meter = Descendants<ChannelLevelMeter>(form).Single();
            using var bitmap = new Bitmap(meter.Width, meter.Height);
            meter.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            var accentPixels = CountPixels(bitmap, DarkUiTheme.Accent);
            return (
                meter.Visible,
                meter.VisualizationEnabled,
                meter.DisplayedChannelCount,
                meter.CurrentFrame?.LayoutName,
                meter.AccessibleDescription,
                accentPixels);
        });

        Assert.True(view.Visible);
        Assert.True(view.VisualizationEnabled);
        Assert.Equal(8, view.DisplayedChannelCount);
        Assert.Equal("7.1", view.LayoutName);
        Assert.Contains("FL Front left", view.AccessibleDescription);
        Assert.Contains("LFE Low-frequency effects", view.AccessibleDescription);
        Assert.Contains("SR Side right", view.AccessibleDescription);
        Assert.True(view.accentPixels > 0);
    }

    [Fact]
    public void LiveChannelVisualizationIsHiddenOutsideForcedDebugMode()
    {
        var snapshot = new AudioStatusSnapshot(
            CurrentStatus: null,
            NextMultichannelRetryAt: null,
            Events: [],
            DebugForceMultichannelSourceEnabled: false);

        var view = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings(), snapshot);
            form.Show();
            var tabs = Descendants<DarkTabControl>(form).Single();
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(page => page.Text == "Status");
            Application.DoEvents();
            var meter = Descendants<ChannelLevelMeter>(form).Single();
            return (meter.Visible, meter.VisualizationEnabled, meter.DisplayedChannelCount);
        });

        Assert.False(view.Visible);
        Assert.False(view.VisualizationEnabled);
        Assert.Equal(0, view.DisplayedChannelCount);
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
            var ambientSlider = Descendants<DarkSlider>(ambient).Single();
            var loudSlider = Descendants<DarkSlider>(loud).Single();
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
    public void SettingsUseTheDarkVisualLanguageAndKeyboardAccessibleSliders()
    {
        var visualLanguage = RunOnStaThread(() =>
        {
            using var form = new SettingsForm(new AppSettings());
            form.Show();
            Application.DoEvents();
            var tabs = Descendants<DarkTabControl>(form).Single();
            tabs.SelectedIndex = 1;
            Application.DoEvents();
            var sliders = Descendants<DarkSlider>(form).ToArray();
            var buttons = Descendants<Button>(form).ToArray();
            var inputs = Descendants<ComboBox>(form).ToArray();
            var checkBoxes = Descendants<CheckBox>(form).ToArray();
            var hotkeyFields = Descendants<HotkeyTextBox>(form).ToArray();
            var overlayPage = tabs.SelectedTab!;
            var overlayPageRight = overlayPage.RectangleToScreen(overlayPage.ClientRectangle).Right;
            var overlayContent = overlayPage.Controls.OfType<TableLayoutPanel>().Single();

            var probeSlider = new DarkSlider { Minimum = 10, Maximum = 90 };
            probeSlider.Value = 200;
            var upperClamp = probeSlider.Value;
            probeSlider.Value = -20;

            return (
                form.BackColor,
                form.ForeColor,
                DarkTabs: 1,
                SliderCount: sliders.Length,
                AllSlidersKeyboardReachable: sliders.All(slider => slider.TabStop),
                AllSlidersShowPercentages: sliders.All(slider => slider.AccessibilityObject.Value?.EndsWith('%') == true),
                AllSlidersFitThePage: sliders.All(slider => slider.RectangleToScreen(slider.ClientRectangle).Right <= overlayPageRight),
                SliderRights: sliders.Select(slider => slider.RectangleToScreen(slider.ClientRectangle).Right).ToArray(),
                OverlayPageRight: overlayPageRight,
                LayoutDetails: $"page client={overlayPage.ClientSize}, padding={overlayPage.Padding}, content={overlayContent.Bounds}, slider={sliders[0].Bounds}",
                HasPrimaryButton: buttons.Any(button => button.BackColor == DarkUiTheme.Accent),
                InputsAreDark: inputs.All(input => input.BackColor == DarkUiTheme.InputBackground),
                AllCheckBoxesUseDarkGlyphs: checkBoxes.All(checkBox => checkBox is DarkCheckBox),
                AllHotkeysCenterTextVertically: hotkeyFields.All(IsTextVerticallyCentered),
                upperClamp,
                lowerClamp: probeSlider.Value);
        });

        Assert.Equal(DarkUiTheme.WindowBackground, visualLanguage.BackColor);
        Assert.Equal(DarkUiTheme.PrimaryText, visualLanguage.ForeColor);
        Assert.Equal(1, visualLanguage.DarkTabs);
        Assert.Equal(3, visualLanguage.SliderCount);
        Assert.True(visualLanguage.AllSlidersKeyboardReachable);
        Assert.True(visualLanguage.AllSlidersShowPercentages);
        Assert.True(
            visualLanguage.AllSlidersFitThePage,
            $"Slider right edges {string.Join(", ", visualLanguage.SliderRights)} exceed page edge {visualLanguage.OverlayPageRight}; {visualLanguage.LayoutDetails}.");
        Assert.True(visualLanguage.HasPrimaryButton);
        Assert.True(visualLanguage.InputsAreDark);
        Assert.True(visualLanguage.AllCheckBoxesUseDarkGlyphs);
        Assert.True(visualLanguage.AllHotkeysCenterTextVertically);
        Assert.Equal(90, visualLanguage.upperClamp);
        Assert.Equal(10, visualLanguage.lowerClamp);
    }

    [Fact]
    public void CheckedDarkCheckBoxRendersADarkMarkOnTheAccentFill()
    {
        var colors = RunOnStaThread(() =>
        {
            using var checkBox = new DarkCheckBox
            {
                BackColor = DarkUiTheme.CardBackground,
                Checked = true,
                Size = new Size(140, 24),
                Text = "Enabled"
            };
            using var bitmap = new Bitmap(checkBox.Width, checkBox.Height);
            checkBox.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));

            var glyph = checkBox.GlyphBounds;
            var accentPixels = 0;
            var darkPixels = 0;
            for (var y = glyph.Top + 2; y < glyph.Bottom - 2; y++)
            {
                for (var x = glyph.Left + 2; x < glyph.Right - 2; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.ToArgb() == DarkUiTheme.Accent.ToArgb())
                    {
                        accentPixels++;
                    }

                    if (pixel.GetBrightness() < 0.15F)
                    {
                        darkPixels++;
                    }
                }
            }

            return (accentPixels, darkPixels);
        });

        Assert.True(colors.accentPixels > 0);
        Assert.True(colors.darkPixels > 0);
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

    private static bool IsTextVerticallyCentered(HotkeyTextBox hotkeyTextBox)
    {
        var textBounds = hotkeyTextBox.TextBounds;
        var interiorTopMargin = textBounds.Top - 1;
        var interiorBottomMargin = hotkeyTextBox.ClientSize.Height - 1 - textBounds.Bottom;
        return Math.Abs(interiorTopMargin - interiorBottomMargin) <= 1;
    }

    private static int CountPixels(Bitmap bitmap, Color color)
    {
        var expected = color.ToArgb();
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == expected)
                {
                    count++;
                }
            }
        }

        return count;
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
