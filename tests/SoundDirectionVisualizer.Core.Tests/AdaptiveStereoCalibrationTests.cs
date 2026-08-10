using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class AdaptiveStereoCalibrationTests
{
    [Fact]
    public void LowVolumeDeviceSignalIsNotRejectedByTheConfiguredThreshold()
    {
        var calibration = new AdaptiveStereoCalibration();

        var effective = calibration.Update(
            new StereoLevels(0.00020, 0.00030),
            configuredSilenceThreshold: 0.00125,
            configuredMaximumBalance: 0.50);
        var estimate = StereoDirectionEstimator.Estimate(
            new StereoLevels(0.00020, 0.00030),
            effective.SilenceRmsThreshold,
            effective.ModelMaximumBalance);

        Assert.Equal(0.00001, effective.SilenceRmsThreshold, precision: 8);
        Assert.False(estimate.IsQuiet);
    }

    [Fact]
    public void FullLevelSignalRetainsTheConfiguredSilenceCeiling()
    {
        var calibration = new AdaptiveStereoCalibration();

        var effective = calibration.Update(
            new StereoLevels(0.20, 0.20),
            configuredSilenceThreshold: 0.00125,
            configuredMaximumBalance: 0.50);

        Assert.Equal(0.00125, effective.SilenceRmsThreshold, precision: 8);
    }

    [Fact]
    public void NarrowStereoMixLearnsMoreUsefulDirectionSensitivity()
    {
        var calibration = new AdaptiveStereoCalibration();
        StereoCalibration effective = default;

        for (var index = 0; index < 128; index++)
        {
            effective = calibration.Update(
                new StereoLevels(0.48, 0.52),
                configuredSilenceThreshold: 0.00125,
                configuredMaximumBalance: 0.50);
        }

        var estimate = StereoDirectionEstimator.Estimate(
            new StereoLevels(0.48, 0.52),
            effective.SilenceRmsThreshold,
            effective.ModelMaximumBalance);

        Assert.InRange(effective.ModelMaximumBalance, 0.041, 0.043);
        Assert.InRange(estimate.CandidateAzimuths[0], 45, 90);
    }

    [Fact]
    public void WideStereoMixDoesNotBecomeOversensitive()
    {
        var calibration = new AdaptiveStereoCalibration();
        StereoCalibration effective = default;

        for (var index = 0; index < 128; index++)
        {
            effective = calibration.Update(
                new StereoLevels(0.25, 0.75),
                configuredSilenceThreshold: 0.00125,
                configuredMaximumBalance: 0.50);
        }

        Assert.InRange(effective.ModelMaximumBalance, 0.49, 0.50);
    }

    [Fact]
    public void SuddenWideTransientMapsToTheLearnedSideReference()
    {
        var calibration = new AdaptiveStereoCalibration();

        for (var index = 0; index < 128; index++)
        {
            calibration.Update(
                new StereoLevels(0.48, 0.52),
                configuredSilenceThreshold: 0.00125,
                configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);
        }

        var levels = new StereoLevels(0.10, 0.90);
        var effective = calibration.Update(
            levels,
            configuredSilenceThreshold: 0.00125,
            configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);
        var estimate = StereoDirectionEstimator.Estimate(
            levels,
            effective.SilenceRmsThreshold,
            effective.ModelMaximumBalance);

        Assert.InRange(effective.ModelMaximumBalance, 0.827, 0.829);
        Assert.Equal(2, estimate.CandidateAzimuths.Count);
        Assert.InRange(estimate.CandidateAzimuths[0], 74.9, 75.1);
        Assert.InRange(estimate.CandidateAzimuths[1], 104.9, 105.1);
    }

    [Fact]
    public void CompressedAndWideEndpointsMapRelativeSideSoundsToSimilarAngles()
    {
        var compressedEndpoint = EstimateAfterCalibration(new StereoLevels(0.10, 0.90));
        var wideEndpoint = EstimateAfterCalibration(new StereoLevels(0.01, 0.99));

        Assert.InRange(compressedEndpoint, 74.9, 75.1);
        Assert.InRange(wideEndpoint, 78, 80);
        Assert.InRange(Math.Abs(wideEndpoint - compressedEndpoint), 0, 4);
    }

    [Fact]
    public void TransientHeadroomReleasesBackTowardTheUsualStereoWidth()
    {
        var calibration = new AdaptiveStereoCalibration();
        var ambient = new StereoLevels(0.48, 0.52);

        for (var index = 0; index < 128; index++)
        {
            calibration.Update(
                ambient,
                configuredSilenceThreshold: 0.00125,
                configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);
        }

        calibration.Update(
            new StereoLevels(0.10, 0.90),
            configuredSilenceThreshold: 0.00125,
            configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);

        StereoCalibration effective = default;
        for (var index = 0; index < 256; index++)
        {
            effective = calibration.Update(
                ambient,
                configuredSilenceThreshold: 0.00125,
                configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);
        }

        Assert.InRange(effective.ModelMaximumBalance, 0.041, 0.043);
    }

    [Fact]
    public void TrueSingleChannelHardPanCanStillReachTheExactSide()
    {
        var calibration = new AdaptiveStereoCalibration();
        var levels = new StereoLevels(0, 1);

        var effective = calibration.Update(
            levels,
            configuredSilenceThreshold: 0.00125,
            configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);
        var estimate = StereoDirectionEstimator.Estimate(
            levels,
            effective.SilenceRmsThreshold,
            effective.ModelMaximumBalance);

        Assert.Equal(1.0, effective.ModelMaximumBalance, precision: 6);
        Assert.Single(estimate.CandidateAzimuths);
        Assert.Equal(90, estimate.CandidateAzimuths[0], precision: 6);
    }

    [Fact]
    public void MinimumStereoWidthPreventsTinyImbalanceFromJumpingToTheSide()
    {
        var calibration = new AdaptiveStereoCalibration();
        StereoCalibration effective = default;

        for (var index = 0; index < 128; index++)
        {
            effective = calibration.Update(
                new StereoLevels(0.4975, 0.5025),
                configuredSilenceThreshold: 0.00125,
                configuredMaximumBalance: 0.50);
        }

        var estimate = StereoDirectionEstimator.Estimate(
            new StereoLevels(0.4975, 0.5025),
            effective.SilenceRmsThreshold,
            effective.ModelMaximumBalance);

        Assert.InRange(estimate.CandidateAzimuths[0], 0, 10);
    }

    [Fact]
    public void ResetDiscardsCalibrationLearnedForThePreviousDevice()
    {
        var calibration = new AdaptiveStereoCalibration();

        for (var index = 0; index < 128; index++)
        {
            calibration.Update(new StereoLevels(0.48, 0.52), 0.00125, 0.50);
        }

        calibration.Reset();
        var effective = calibration.Update(new StereoLevels(0.48, 0.52), 0.00125, 0.50);

        Assert.Equal(0.08, effective.ModelMaximumBalance, precision: 6);
    }

    private static double EstimateAfterCalibration(StereoLevels levels)
    {
        var calibration = new AdaptiveStereoCalibration();
        var effective = calibration.Update(
            levels,
            configuredSilenceThreshold: 0.00125,
            configuredMaximumBalance: AdaptiveStereoCalibration.TheoreticalMaximumBalance);
        var estimate = StereoDirectionEstimator.Estimate(
            levels,
            effective.SilenceRmsThreshold,
            effective.ModelMaximumBalance);

        return estimate.CandidateAzimuths[0];
    }
}
