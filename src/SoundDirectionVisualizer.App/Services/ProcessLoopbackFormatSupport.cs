using NAudio.Wave;
using SoundDirectionVisualizer.Core.Audio;
using System.Numerics;

namespace SoundDirectionVisualizer.App.Services;

internal sealed record ProcessLoopbackFormatOption(
    string LayoutName,
    ChannelLayout Layout,
    WaveFormatExtensible WaveFormat);

internal static class ProcessLoopbackFormatSupport
{
    public const int SampleRate = 48_000;
    public const int BitsPerSample = 32;

    public static IReadOnlyList<ProcessLoopbackFormatOption> CreateMultichannelCandidates() =>
    [
        new(
            ChannelLayout.Surround71.Name,
            ChannelLayout.Surround71,
            new WaveFormatExtensible(
                SampleRate,
                BitsPerSample,
                ChannelLayout.Surround71.ChannelCount,
                true,
                BitsPerSample,
                Speakers.Surround71)),
        new(
            ChannelLayout.Surround51.Name,
            ChannelLayout.Surround51,
            new WaveFormatExtensible(
                SampleRate,
                BitsPerSample,
                ChannelLayout.Surround51.ChannelCount,
                true,
                BitsPerSample,
                Speakers.Surround51))
    ];

    public static bool TryResolveLayout(WaveFormat format, out ChannelLayout? layout, out string reason)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (format is not WaveFormatExtensible extensible)
        {
            layout = null;
            reason = "The multichannel stream did not provide a WAVEFORMATEXTENSIBLE channel mask.";
            return false;
        }

        var channelMask = unchecked((uint)extensible.ChannelMask);
        if (BitOperations.PopCount(channelMask) != format.Channels)
        {
            layout = null;
            reason = $"The channel mask contains {BitOperations.PopCount(channelMask)} positions for {format.Channels} channels.";
            return false;
        }

        if (channelMask == unchecked((uint)Speakers.Surround71)
            && format.Channels == ChannelLayout.Surround71.ChannelCount)
        {
            layout = ChannelLayout.Surround71;
            reason = string.Empty;
            return true;
        }

        if (channelMask == unchecked((uint)Speakers.Surround51)
            && format.Channels == ChannelLayout.Surround51.ChannelCount)
        {
            layout = ChannelLayout.Surround51;
            reason = string.Empty;
            return true;
        }

        layout = null;
        reason = $"Unsupported {format.Channels}-channel mask 0x{channelMask:X}.";
        return false;
    }
}
