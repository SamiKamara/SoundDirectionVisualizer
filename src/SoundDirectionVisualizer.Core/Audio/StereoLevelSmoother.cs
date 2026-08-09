namespace SoundDirectionVisualizer.Core.Audio;

public sealed class StereoLevelSmoother
{
    private StereoLevels _current;
    private bool _hasValue;

    public StereoLevels Update(StereoLevels input, double factor)
    {
        factor = Math.Clamp(factor, 0.01, 1.0);

        if (!_hasValue)
        {
            _current = input;
            _hasValue = true;
            return _current;
        }

        _current = new StereoLevels(
            _current.LeftRms + factor * (input.LeftRms - _current.LeftRms),
            _current.RightRms + factor * (input.RightRms - _current.RightRms));

        return _current;
    }

    public void Reset()
    {
        _current = default;
        _hasValue = false;
    }
}
