using System.Drawing;

namespace SoundDirectionVisualizer.App;

public sealed class AppSettings
{
    public bool OverlayEnabled { get; set; } = true;

    public string? AudioDeviceId { get; set; }

    public bool AutomaticAudioCalibration { get; set; } = true;

    public double SilenceRmsThreshold { get; set; } = 0.00125;

    public double SmoothingFactor { get; set; } = 0.20;

    public double ModelMaximumBalance { get; set; } = 0.50;

    public bool AutoDetectSteamGameMonitor { get; set; } = true;

    public string? SelectedMonitorDeviceName { get; set; }

    public string OverlayColorHex { get; set; } = "#FFFFFF";

    public int OverlayOpacityPercent { get; set; } = -1;

    public int OverlayHeightPercent { get; set; }

    public int RingThickness { get; set; } = 3;

    public int MarkerSize { get; set; } = 8;

    public int HorizontalOffset { get; set; }

    public int VerticalOffset { get; set; }

    public bool ShowCompassRing { get; set; }

    public bool ShowCardinalTicks { get; set; }

    public bool ShowCurrentDirectionRays { get; set; }

    public bool ShowCurrentDirectionMarkers { get; set; } = true;

    public bool ShowListenerDot { get; set; }

    public bool ShowDirectionTrail { get; set; } = true;

    public double TrailDurationSeconds { get; set; } = 5;

    public bool ShowCompassLabels { get; set; }

    public HotkeyDefinition ToggleHotkey { get; set; } = HotkeyDefinition.DefaultToggle();

    public HotkeyDefinition CycleMonitorHotkey { get; set; } = HotkeyDefinition.DefaultCycle();

    public HotkeyDefinition OpenSettingsHotkey { get; set; } = HotkeyDefinition.DefaultOpenSettings();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            OverlayEnabled = OverlayEnabled,
            AudioDeviceId = AudioDeviceId,
            AutomaticAudioCalibration = AutomaticAudioCalibration,
            SilenceRmsThreshold = SilenceRmsThreshold,
            SmoothingFactor = SmoothingFactor,
            ModelMaximumBalance = ModelMaximumBalance,
            AutoDetectSteamGameMonitor = AutoDetectSteamGameMonitor,
            SelectedMonitorDeviceName = SelectedMonitorDeviceName,
            OverlayColorHex = OverlayColorHex,
            OverlayOpacityPercent = OverlayOpacityPercent,
            OverlayHeightPercent = OverlayHeightPercent,
            RingThickness = RingThickness,
            MarkerSize = MarkerSize,
            HorizontalOffset = HorizontalOffset,
            VerticalOffset = VerticalOffset,
            ShowCompassRing = ShowCompassRing,
            ShowCardinalTicks = ShowCardinalTicks,
            ShowCurrentDirectionRays = ShowCurrentDirectionRays,
            ShowCurrentDirectionMarkers = ShowCurrentDirectionMarkers,
            ShowListenerDot = ShowListenerDot,
            ShowDirectionTrail = ShowDirectionTrail,
            TrailDurationSeconds = TrailDurationSeconds,
            ShowCompassLabels = ShowCompassLabels,
            ToggleHotkey = ToggleHotkey.Clone(),
            CycleMonitorHotkey = CycleMonitorHotkey.Clone(),
            OpenSettingsHotkey = OpenSettingsHotkey.Clone()
        };
    }

    public void Normalize()
    {
        SilenceRmsThreshold = Math.Clamp(SilenceRmsThreshold, 0.00001, 0.1);
        SmoothingFactor = Math.Clamp(SmoothingFactor, 0.01, 1);
        ModelMaximumBalance = Math.Clamp(ModelMaximumBalance, 0.05, 1);
        OverlayOpacityPercent = OverlayOpacityPercent < 0
            ? 40
            : Math.Clamp(OverlayOpacityPercent, 0, 100);

        if (OverlayHeightPercent == 0)
        {
            OverlayHeightPercent = 110;

            // Version 1 originally placed its small fixed-size overlay below center.
            // Screen-relative sizing is centered so the top and bottom margins match.
            if (VerticalOffset == 220)
            {
                VerticalOffset = 0;
            }
        }

        OverlayHeightPercent = Math.Clamp(OverlayHeightPercent, 10, 200);
        RingThickness = Math.Clamp(RingThickness, 1, 12);
        MarkerSize = Math.Clamp(MarkerSize, 4, 32);
        HorizontalOffset = Math.Clamp(HorizontalOffset, -4000, 4000);
        VerticalOffset = Math.Clamp(VerticalOffset, -4000, 4000);
        TrailDurationSeconds = Math.Clamp(TrailDurationSeconds, 0.5, 15);

        ToggleHotkey ??= HotkeyDefinition.DefaultToggle();
        CycleMonitorHotkey ??= HotkeyDefinition.DefaultCycle();
        OpenSettingsHotkey ??= HotkeyDefinition.DefaultOpenSettings();

        if (!ToggleHotkey.IsValid)
        {
            ToggleHotkey = HotkeyDefinition.DefaultToggle();
        }

        if (!CycleMonitorHotkey.IsEmpty && !CycleMonitorHotkey.IsValid)
        {
            CycleMonitorHotkey = HotkeyDefinition.DefaultCycle();
        }

        if (!OpenSettingsHotkey.IsEmpty && !OpenSettingsHotkey.IsValid)
        {
            OpenSettingsHotkey = HotkeyDefinition.DefaultOpenSettings();
        }

        try
        {
            _ = ColorTranslator.FromHtml(OverlayColorHex);
        }
        catch
        {
            OverlayColorHex = "#FFFFFF";
        }
    }

    public Color GetOverlayColor()
    {
        Normalize();
        var color = ColorTranslator.FromHtml(OverlayColorHex);
        return Color.FromArgb(255, color);
    }
}
