using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Direction;

public readonly record struct StereoCalibration(
    double SilenceRmsThreshold,
    double ModelMaximumBalance);

public sealed class AdaptiveStereoCalibration
{
    public const double TheoreticalMaximumBalance = 1.0;

    private const int BalanceWindowSize = 256;
    private const int RecalculationInterval = 8;
    private const double MinimumSilenceThreshold = 0.00001;
    private const double PeakThresholdRatio = 0.005;
    private const double PeakReleaseFactor = 0.01;
    private const double InitialMaximumBalance = 0.08;
    private const double MinimumMaximumBalance = 0.03;
    private const double WidthPercentile = 0.90;
    private const double LearnedSideReferenceDegrees = 75;
    private const double WidthAdjustmentFactor = 0.25;

    private static readonly double WidthHeadroom = 1 / Math.Sin(
        LearnedSideReferenceDegrees * Math.PI / 180);

    private readonly double[] _balanceWindow = new double[BalanceWindowSize];
    private readonly double[] _sortBuffer = new double[BalanceWindowSize];
    private double _recentPeak;
    private double _effectiveMaximumBalance = InitialMaximumBalance;
    private int _balanceCount;
    private int _balanceIndex;
    private int _samplesSinceRecalculation;

    public StereoCalibration Update(
        StereoLevels levels,
        double configuredSilenceThreshold,
        double configuredMaximumBalance)
    {
        configuredSilenceThreshold = Math.Clamp(configuredSilenceThreshold, MinimumSilenceThreshold, 1);
        configuredMaximumBalance = Math.Clamp(
            configuredMaximumBalance,
            MinimumMaximumBalance,
            TheoreticalMaximumBalance);

        var total = Math.Max(0, levels.CombinedRms);
        UpdateRecentPeak(total);

        var effectiveSilenceThreshold = Math.Min(
            configuredSilenceThreshold,
            Math.Max(MinimumSilenceThreshold, _recentPeak * PeakThresholdRatio));

        if (total >= effectiveSilenceThreshold)
        {
            var absoluteBalance = Math.Abs((levels.RightRms - levels.LeftRms) / (total + double.Epsilon));
            AddBalanceSample(Math.Clamp(absoluteBalance, 0, 1), configuredMaximumBalance);
        }

        return new StereoCalibration(
            effectiveSilenceThreshold,
            Math.Clamp(_effectiveMaximumBalance, MinimumMaximumBalance, configuredMaximumBalance));
    }

    public void Reset()
    {
        Array.Clear(_balanceWindow);
        Array.Clear(_sortBuffer);
        _recentPeak = 0;
        _effectiveMaximumBalance = InitialMaximumBalance;
        _balanceCount = 0;
        _balanceIndex = 0;
        _samplesSinceRecalculation = 0;
    }

    private void UpdateRecentPeak(double total)
    {
        if (total >= _recentPeak)
        {
            _recentPeak = total;
            return;
        }

        _recentPeak += PeakReleaseFactor * (total - _recentPeak);
    }

    private void AddBalanceSample(double absoluteBalance, double configuredMaximumBalance)
    {
        // A wider transient must gain headroom before this same frame is estimated;
        // the percentile recalculation below then provides the slower release.
        var transientMaximum = Math.Clamp(
            absoluteBalance * WidthHeadroom,
            MinimumMaximumBalance,
            configuredMaximumBalance);
        _effectiveMaximumBalance = Math.Max(_effectiveMaximumBalance, transientMaximum);

        _balanceWindow[_balanceIndex] = absoluteBalance;
        _balanceIndex = (_balanceIndex + 1) % BalanceWindowSize;
        _balanceCount = Math.Min(_balanceCount + 1, BalanceWindowSize);
        _samplesSinceRecalculation++;

        if (_samplesSinceRecalculation < RecalculationInterval)
        {
            return;
        }

        _samplesSinceRecalculation = 0;
        Array.Copy(_balanceWindow, _sortBuffer, _balanceCount);
        Array.Sort(_sortBuffer, 0, _balanceCount);

        var percentileIndex = Math.Clamp(
            (int)Math.Ceiling(_balanceCount * WidthPercentile) - 1,
            0,
            _balanceCount - 1);
        var observedMaximum = _sortBuffer[percentileIndex] * WidthHeadroom;
        var targetMaximum = Math.Clamp(
            observedMaximum,
            MinimumMaximumBalance,
            configuredMaximumBalance);

        _effectiveMaximumBalance += WidthAdjustmentFactor
            * (targetMaximum - _effectiveMaximumBalance);
    }
}
