namespace SoundDirectionVisualizer.Core.Audio;

public sealed class ChannelLevelSmoother
{
    private ChannelLayout? _layout;
    private double[]? _current;

    public ChannelLevels Update(ChannelLevels input, double factor)
    {
        ArgumentNullException.ThrowIfNull(input);
        factor = Math.Clamp(factor, 0.01, 1);

        if (_layout is null || _current is null || !_layout.HasSamePositions(input.Layout))
        {
            _layout = input.Layout;
            _current = input.RmsLevels.ToArray();
            return new ChannelLevels(_layout, _current);
        }

        for (var index = 0; index < _current.Length; index++)
        {
            _current[index] += factor * (input.RmsLevels[index] - _current[index]);
        }

        return new ChannelLevels(_layout, _current);
    }

    public void Reset()
    {
        _layout = null;
        _current = null;
    }
}
