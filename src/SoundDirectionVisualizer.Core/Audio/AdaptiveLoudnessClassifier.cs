namespace SoundDirectionVisualizer.Core.Audio;

public sealed class AdaptiveLoudnessClassifier
{
    private const int LevelWindowSize = 256;
    private const int MinimumBaselineSamples = 32;
    private const int RecalculationInterval = 8;
    private const double BaselinePercentile = 0.50;

    private readonly double[] _levelWindow = new double[LevelWindowSize];
    private readonly double[] _sortBuffer = new double[LevelWindowSize];
    private double _ambientBaseline;
    private int _levelCount;
    private int _levelIndex;
    private int _samplesSinceRecalculation;

    public SoundLoudness Update(
        StereoLevels levels,
        double silenceRmsThreshold,
        double loudnessMultiplier)
    {
        silenceRmsThreshold = Math.Clamp(silenceRmsThreshold, 0.00001, 1);
        loudnessMultiplier = Math.Clamp(loudnessMultiplier, 1.1, 10);

        var total = Math.Max(0, levels.CombinedRms);
        var hasBaseline = _levelCount >= MinimumBaselineSamples && _ambientBaseline > 0;
        var loudThreshold = Math.Max(
            _ambientBaseline * loudnessMultiplier,
            silenceRmsThreshold * loudnessMultiplier);
        var result = hasBaseline && total >= loudThreshold
            ? SoundLoudness.Loud
            : SoundLoudness.Ambient;

        if (total >= silenceRmsThreshold)
        {
            AddLevelSample(total);
        }

        return result;
    }

    public void Reset()
    {
        Array.Clear(_levelWindow);
        Array.Clear(_sortBuffer);
        _ambientBaseline = 0;
        _levelCount = 0;
        _levelIndex = 0;
        _samplesSinceRecalculation = 0;
    }

    private void AddLevelSample(double level)
    {
        _levelWindow[_levelIndex] = level;
        _levelIndex = (_levelIndex + 1) % LevelWindowSize;
        _levelCount = Math.Min(_levelCount + 1, LevelWindowSize);
        _samplesSinceRecalculation++;

        if (_samplesSinceRecalculation < RecalculationInterval)
        {
            return;
        }

        _samplesSinceRecalculation = 0;
        Array.Copy(_levelWindow, _sortBuffer, _levelCount);
        Array.Sort(_sortBuffer, 0, _levelCount);

        var percentileIndex = Math.Clamp(
            (int)Math.Ceiling(_levelCount * BaselinePercentile) - 1,
            0,
            _levelCount - 1);
        _ambientBaseline = _sortBuffer[percentileIndex];
    }
}
