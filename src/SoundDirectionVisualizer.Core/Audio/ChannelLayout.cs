using System.Collections.ObjectModel;

namespace SoundDirectionVisualizer.Core.Audio;

public sealed class ChannelLayout
{
    private readonly ReadOnlyCollection<SpeakerPosition> _positions;

    public ChannelLayout(string name, params SpeakerPosition[] positions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Length < 3)
        {
            throw new ArgumentException("A multichannel layout requires at least three channels.", nameof(positions));
        }

        if (positions.Distinct().Count() != positions.Length)
        {
            throw new ArgumentException("A channel layout cannot contain duplicate speaker positions.", nameof(positions));
        }

        Name = name;
        _positions = Array.AsReadOnly((SpeakerPosition[])positions.Clone());
    }

    public static ChannelLayout Surround51 { get; } = new(
        "5.1",
        SpeakerPosition.FrontLeft,
        SpeakerPosition.FrontRight,
        SpeakerPosition.FrontCenter,
        SpeakerPosition.LowFrequency,
        SpeakerPosition.SideLeft,
        SpeakerPosition.SideRight);

    public static ChannelLayout Surround71 { get; } = new(
        "7.1",
        SpeakerPosition.FrontLeft,
        SpeakerPosition.FrontRight,
        SpeakerPosition.FrontCenter,
        SpeakerPosition.LowFrequency,
        SpeakerPosition.BackLeft,
        SpeakerPosition.BackRight,
        SpeakerPosition.SideLeft,
        SpeakerPosition.SideRight);

    public string Name { get; }

    public int ChannelCount => _positions.Count;

    public IReadOnlyList<SpeakerPosition> Positions => _positions;

    public int IndexOf(SpeakerPosition position)
    {
        for (var index = 0; index < _positions.Count; index++)
        {
            if (_positions[index] == position)
            {
                return index;
            }
        }

        return -1;
    }

    public bool HasSamePositions(ChannelLayout other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _positions.SequenceEqual(other._positions);
    }
}
