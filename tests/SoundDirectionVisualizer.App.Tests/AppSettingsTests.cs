using SoundDirectionVisualizer.App;
using System.Text.Json;

namespace SoundDirectionVisualizer.App.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void NewSettingsUseRequestedAudioAndOverlayDefaults()
    {
        var settings = new AppSettings();

        settings.Normalize();

        Assert.True(settings.OverlayEnabled);
        Assert.False(settings.UseDetectedGameProcessAudio);
        Assert.True(settings.UseBestAvailableMultichannelAudio);
        Assert.False(settings.DebugForceMultichannelSource);
        Assert.True(settings.AutomaticallyFallbackToGameProcessAudio);
        Assert.True(settings.AutomaticAudioCalibration);
        Assert.Equal(0.50, settings.ModelMaximumBalance, precision: 6);
        Assert.True(settings.LoudSoundEmphasisEnabled);
        Assert.Equal(2.5, settings.LoudSoundThresholdMultiplier, precision: 6);
        Assert.Equal("#FFFFFF", settings.OverlayColorHex);
        Assert.Equal(40, settings.OverlayOpacityPercent);
        Assert.Equal(110, settings.OverlayHeightPercent);
        Assert.Equal(3, settings.RingThickness);
        Assert.Equal(8, settings.MarkerSize);
        Assert.Equal(60, settings.AmbientMarkerSizePercent);
        Assert.Equal(40, settings.AmbientMarkerOpacityPercent);
        Assert.Equal("#FFFFFF", settings.AmbientMarkerColorHex);
        Assert.Equal(160, settings.LoudMarkerSizePercent);
        Assert.Equal(100, settings.LoudMarkerOpacityPercent);
        Assert.Equal("#FFFFFF", settings.LoudMarkerColorHex);
        Assert.True(settings.LoudMarkerOutlineEnabled);
        Assert.Equal("#000000", settings.LoudMarkerOutlineColorHex);
        Assert.Equal(0.8, settings.LoudMarkerOutlineThickness, precision: 6);
        Assert.Equal(0, settings.HorizontalOffset);
        Assert.Equal(0, settings.VerticalOffset);
        Assert.False(settings.ShowCompassRing);
        Assert.False(settings.ShowCardinalTicks);
        Assert.False(settings.ShowCurrentDirectionRays);
        Assert.True(settings.ShowCurrentDirectionMarkers);
        Assert.False(settings.ShowListenerDot);
        Assert.True(settings.ShowDirectionTrail);
        Assert.Equal(5, settings.TrailDurationSeconds);
        Assert.False(settings.ShowCompassLabels);
    }

    [Fact]
    public void LegacySmallOverlayOffsetIsCenteredWhenDisplayRelativeSizingIsIntroduced()
    {
        var settings = new AppSettings
        {
            OverlayHeightPercent = 0,
            OverlayOpacityPercent = -1,
            VerticalOffset = 220
        };

        settings.Normalize();

        Assert.Equal(110, settings.OverlayHeightPercent);
        Assert.Equal(40, settings.OverlayOpacityPercent);
        Assert.Equal(0, settings.VerticalOffset);
    }

    [Fact]
    public void ExistingSettingsWithoutCalibrationFieldEnableAutomaticCalibration()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"OverlayEnabled\":true}");

        Assert.NotNull(settings);
        Assert.True(settings.AutomaticAudioCalibration);
    }

    [Fact]
    public void MarkerAppearanceSettingsAreNormalizedAndCloned()
    {
        var settings = new AppSettings
        {
            LoudSoundEmphasisEnabled = false,
            LoudSoundThresholdMultiplier = 50,
            OverlayColorHex = "#123456",
            AmbientMarkerSizePercent = 0,
            AmbientMarkerOpacityPercent = -1,
            AmbientMarkerColorHex = "not-a-color",
            LoudMarkerSizePercent = 500,
            LoudMarkerOpacityPercent = 0,
            LoudMarkerColorHex = "#ABCDEF",
            LoudMarkerOutlineEnabled = false,
            LoudMarkerOutlineColorHex = "not-a-color",
            LoudMarkerOutlineThickness = 20
        };

        settings.Normalize();
        var clone = settings.Clone();

        Assert.False(clone.LoudSoundEmphasisEnabled);
        Assert.Equal(10, clone.LoudSoundThresholdMultiplier, precision: 6);
        Assert.Equal(25, clone.AmbientMarkerSizePercent);
        Assert.Equal(10, clone.AmbientMarkerOpacityPercent);
        Assert.Equal("#123456", clone.AmbientMarkerColorHex);
        Assert.Equal(300, clone.LoudMarkerSizePercent);
        Assert.Equal(10, clone.LoudMarkerOpacityPercent);
        Assert.Equal("#ABCDEF", clone.LoudMarkerColorHex);
        Assert.False(clone.LoudMarkerOutlineEnabled);
        Assert.Equal("#000000", clone.LoudMarkerOutlineColorHex);
        Assert.Equal(8, clone.LoudMarkerOutlineThickness);
    }

    [Fact]
    public void ExistingSettingsWithoutMarkerColorsInheritTheOverlayColor()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"OverlayColorHex\":\"#12ab34\"}");

        Assert.NotNull(settings);
        settings.Normalize();

        Assert.Equal("#12AB34", settings.AmbientMarkerColorHex);
        Assert.Equal("#12AB34", settings.LoudMarkerColorHex);
    }

    [Fact]
    public void LoudOutlineThicknessUsesTenthPixelPrecision()
    {
        var settings = new AppSettings { LoudMarkerOutlineThickness = 0.36 };

        settings.Normalize();
        var clone = settings.Clone();

        Assert.Equal(0.4, clone.LoudMarkerOutlineThickness, precision: 6);
    }

    [Fact]
    public void LegacyGameAudioPreferenceMigratesBackToEndpointCapture()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(
            "{\"OverlayEnabled\":true,\"PreferDetectedGameAudio\":true}");

        Assert.NotNull(settings);
        settings.Normalize();

        Assert.False(settings.UseDetectedGameProcessAudio);
        Assert.False(settings.LegacyPreferDetectedGameAudio);
    }

    [Fact]
    public void ClonePreservesOptionalGameProcessAudioCapture()
    {
        var settings = new AppSettings
        {
            UseDetectedGameProcessAudio = true,
            UseBestAvailableMultichannelAudio = false,
            DebugForceMultichannelSource = true,
            AutomaticallyFallbackToGameProcessAudio = false
        };

        var clone = settings.Clone();

        Assert.True(clone.UseDetectedGameProcessAudio);
        Assert.False(clone.UseBestAvailableMultichannelAudio);
        Assert.True(clone.DebugForceMultichannelSource);
        Assert.False(clone.AutomaticallyFallbackToGameProcessAudio);
    }

    [Fact]
    public void ExistingSettingsEnableAutomaticCenteredOutputFallback()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"OverlayEnabled\":true}");

        Assert.NotNull(settings);
        Assert.True(settings.AutomaticallyFallbackToGameProcessAudio);
    }

    [Fact]
    public void ExistingSettingsEnableBestAvailableMultichannelAudio()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"OverlayEnabled\":true}");

        Assert.NotNull(settings);
        Assert.True(settings.UseBestAvailableMultichannelAudio);
    }

    [Fact]
    public void SavingUsesOnlyTheNewOptionalProcessCaptureSetting()
    {
        var settings = new AppSettings
        {
            UseDetectedGameProcessAudio = true,
            LegacyPreferDetectedGameAudio = true
        };
        settings.Normalize();

        var json = JsonSerializer.Serialize(settings);

        Assert.Contains("\"UseDetectedGameProcessAudio\":true", json);
        Assert.DoesNotContain("PreferDetectedGameAudio", json);
    }
}
