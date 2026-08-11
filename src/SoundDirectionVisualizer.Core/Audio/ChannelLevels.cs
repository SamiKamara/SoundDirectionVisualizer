using System.Collections.ObjectModel;

namespace SoundDirectionVisualizer.Core.Audio;

public sealed class ChannelLevels
{
    private readonly double[] _levels;
    private readonly ReadOnlyCollection<double> _readOnlyLevels;

    public ChannelLevels(ChannelLayout layout, IReadOnlyList<double> rmsLevels)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rmsLevels);

        if (rmsLevels.Count != layout.ChannelCount)
        {
            throw new ArgumentException(
                $"Layout {layout.Name} requires {layout.ChannelCount} levels; received {rmsLevels.Count}.",
                nameof(rmsLevels));
        }

        _levels = new double[rmsLevels.Count];
        for (var index = 0; index < rmsLevels.Count; index++)
        {
            var level = rmsLevels[index];
            if (!double.IsFinite(level) || level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rmsLevels), "RMS levels must be finite and non-negative.");
            }

            _levels[index] = level;
        }

        Layout = layout;
        _readOnlyLevels = Array.AsReadOnly(_levels);
    }

    public ChannelLayout Layout { get; }

    public IReadOnlyList<double> RmsLevels => _readOnlyLevels;

    public double GetRms(SpeakerPosition position)
    {
        var index = Layout.IndexOf(position);
        return index < 0 ? 0 : _levels[index];
    }

    public StereoLevels ToStereoFallback()
    {
        double leftEnergy = 0;
        double rightEnergy = 0;

        for (var index = 0; index < _levels.Length; index++)
        {
            var energy = _levels[index] * _levels[index];
            switch (Layout.Positions[index])
            {
                case SpeakerPosition.FrontLeft:
                case SpeakerPosition.FrontLeftOfCenter:
                case SpeakerPosition.BackLeft:
                case SpeakerPosition.SideLeft:
                    leftEnergy += energy;
                    break;
                case SpeakerPosition.FrontRight:
                case SpeakerPosition.FrontRightOfCenter:
                case SpeakerPosition.BackRight:
                case SpeakerPosition.SideRight:
                    rightEnergy += energy;
                    break;
                case SpeakerPosition.FrontCenter:
                case SpeakerPosition.BackCenter:
                    leftEnergy += energy * 0.5;
                    rightEnergy += energy * 0.5;
                    break;
                case SpeakerPosition.LowFrequency:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return new StereoLevels(Math.Sqrt(leftEnergy), Math.Sqrt(rightEnergy));
    }
}
