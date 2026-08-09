namespace SoundDirectionVisualizer.Core.Audio;

public readonly record struct StereoLevels(double LeftRms, double RightRms)
{
    public double CombinedRms => LeftRms + RightRms;
}
