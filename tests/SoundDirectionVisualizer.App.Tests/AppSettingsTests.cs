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
        Assert.True(settings.PreferDetectedGameAudio);
        Assert.True(settings.AutomaticAudioCalibration);
        Assert.Equal(0.50, settings.ModelMaximumBalance, precision: 6);
        Assert.True(settings.LoudSoundEmphasisEnabled);
        Assert.Equal(2.5, settings.LoudSoundThresholdMultiplier, precision: 6);
        Assert.Equal("#FFFFFF", settings.OverlayColorHex);
        Assert.Equal(40, settings.OverlayOpacityPercent);
        Assert.Equal(110, settings.OverlayHeightPercent);
        Assert.Equal(3, settings.RingThickness);
        Assert.Equal(8, settings.MarkerSize);
        Assert.Equal(70, settings.AmbientMarkerOpacityPercent);
        Assert.Equal(150, settings.LoudMarkerSizePercent);
        Assert.Equal(100, settings.LoudMarkerOpacityPercent);
        Assert.True(settings.LoudMarkerOutlineEnabled);
        Assert.Equal("#000000", settings.LoudMarkerOutlineColorHex);
        Assert.Equal(2, settings.LoudMarkerOutlineThickness);
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
    public void LoudMarkerSettingsAreNormalizedAndCloned()
    {
        var settings = new AppSettings
        {
            LoudSoundEmphasisEnabled = false,
            LoudSoundThresholdMultiplier = 50,
            AmbientMarkerOpacityPercent = -1,
            LoudMarkerSizePercent = 500,
            LoudMarkerOpacityPercent = 0,
            LoudMarkerOutlineEnabled = false,
            LoudMarkerOutlineColorHex = "not-a-color",
            LoudMarkerOutlineThickness = 20
        };

        settings.Normalize();
        var clone = settings.Clone();

        Assert.False(clone.LoudSoundEmphasisEnabled);
        Assert.Equal(10, clone.LoudSoundThresholdMultiplier, precision: 6);
        Assert.Equal(10, clone.AmbientMarkerOpacityPercent);
        Assert.Equal(300, clone.LoudMarkerSizePercent);
        Assert.Equal(10, clone.LoudMarkerOpacityPercent);
        Assert.False(clone.LoudMarkerOutlineEnabled);
        Assert.Equal("#000000", clone.LoudMarkerOutlineColorHex);
        Assert.Equal(8, clone.LoudMarkerOutlineThickness);
    }

    [Fact]
    public void ExistingSettingsWithoutGameAudioPreferencePreferDetectedGameAudio()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"OverlayEnabled\":true}");

        Assert.NotNull(settings);
        Assert.True(settings.PreferDetectedGameAudio);
    }

    [Fact]
    public void ClonePreservesGameAudioPreference()
    {
        var settings = new AppSettings { PreferDetectedGameAudio = false };

        var clone = settings.Clone();

        Assert.False(clone.PreferDetectedGameAudio);
    }
}
