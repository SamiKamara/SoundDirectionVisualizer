using SoundDirectionVisualizer.Core.Audio;
using System.Collections.ObjectModel;

namespace SoundDirectionVisualizer.App.Services;

public sealed record AudioChannelMeterChannel
{
    public AudioChannelMeterChannel(
        SpeakerPosition position,
        string shortLabel,
        string displayName,
        double rmsLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!double.IsFinite(rmsLevel) || rmsLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rmsLevel));
        }

        Position = position;
        ShortLabel = shortLabel;
        DisplayName = displayName;
        RmsLevel = rmsLevel;
    }

    public SpeakerPosition Position { get; }

    public string ShortLabel { get; }

    public string DisplayName { get; }

    public double RmsLevel { get; }
}

public sealed class AudioChannelMeterFrame
{
    private readonly ReadOnlyCollection<AudioChannelMeterChannel> _channels;

    public AudioChannelMeterFrame(
        DateTimeOffset timestamp,
        string sourceName,
        string layoutName,
        IReadOnlyList<AudioChannelMeterChannel> channels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutName);
        ArgumentNullException.ThrowIfNull(channels);
        if (channels.Count == 0)
        {
            throw new ArgumentException("At least one monitored channel is required.", nameof(channels));
        }

        Timestamp = timestamp;
        SourceName = sourceName;
        LayoutName = layoutName;
        _channels = Array.AsReadOnly(channels.ToArray());
    }

    public DateTimeOffset Timestamp { get; }

    public string SourceName { get; }

    public string LayoutName { get; }

    public IReadOnlyList<AudioChannelMeterChannel> Channels => _channels;
}

internal static class AudioChannelMeterFrameFactory
{
    public static AudioChannelMeterFrame FromStereo(
        DateTimeOffset timestamp,
        string sourceName,
        StereoLevels levels) => new(
            timestamp,
            sourceName,
            "Stereo",
            [
                CreateChannel(SpeakerPosition.FrontLeft, levels.LeftRms),
                CreateChannel(SpeakerPosition.FrontRight, levels.RightRms)
            ]);

    public static AudioChannelMeterFrame FromMultichannel(
        DateTimeOffset timestamp,
        string sourceName,
        ChannelLevels levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        var channels = new AudioChannelMeterChannel[levels.Layout.ChannelCount];
        for (var index = 0; index < channels.Length; index++)
        {
            channels[index] = CreateChannel(
                levels.Layout.Positions[index],
                levels.RmsLevels[index]);
        }

        return new AudioChannelMeterFrame(
            timestamp,
            sourceName,
            levels.Layout.Name,
            channels);
    }

    private static AudioChannelMeterChannel CreateChannel(
        SpeakerPosition position,
        double rmsLevel)
    {
        var (shortLabel, displayName) = position switch
        {
            SpeakerPosition.FrontLeft => ("FL", "Front left"),
            SpeakerPosition.FrontRight => ("FR", "Front right"),
            SpeakerPosition.FrontCenter => ("FC", "Front center"),
            SpeakerPosition.LowFrequency => ("LFE", "Low-frequency effects"),
            SpeakerPosition.BackLeft => ("BL", "Back left"),
            SpeakerPosition.BackRight => ("BR", "Back right"),
            SpeakerPosition.FrontLeftOfCenter => ("FLC", "Front left of center"),
            SpeakerPosition.FrontRightOfCenter => ("FRC", "Front right of center"),
            SpeakerPosition.BackCenter => ("BC", "Back center"),
            SpeakerPosition.SideLeft => ("SL", "Side left"),
            SpeakerPosition.SideRight => ("SR", "Side right"),
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
        return new AudioChannelMeterChannel(position, shortLabel, displayName, rmsLevel);
    }
}

internal static class AudioChannelMeterScale
{
    internal const double MinimumDecibels = -60;

    public static double ToDecibels(double rmsLevel)
    {
        if (!double.IsFinite(rmsLevel) || rmsLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rmsLevel));
        }

        return rmsLevel <= 0
            ? double.NegativeInfinity
            : 20 * Math.Log10(rmsLevel);
    }

    public static double ToNormalizedWidth(double rmsLevel)
    {
        var decibels = ToDecibels(rmsLevel);
        if (double.IsNegativeInfinity(decibels))
        {
            return 0;
        }

        return Math.Clamp(
            (decibels - MinimumDecibels) / -MinimumDecibels,
            0,
            1);
    }
}
