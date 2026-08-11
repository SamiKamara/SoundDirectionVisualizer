using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class MultichannelContentValidatorTests
{
    [Fact]
    public void VerifiesRepeatedIndependentSurroundContent()
    {
        var validator = new MultichannelContentValidator();
        var analysis = CreateAnalysis(sideRms: 0.5, sideIndependence: 1);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        Assert.Equal(MultichannelValidationState.Pending, validator.Observe(start, analysis));
        Assert.Equal(MultichannelValidationState.Pending, validator.Observe(start.AddMilliseconds(100), analysis));
        Assert.Equal(MultichannelValidationState.Verified, validator.Observe(start.AddMilliseconds(200), analysis));
    }

    [Fact]
    public void MarksDuplicatedOrStereoDerivedSurroundAsUninformativeAfterBoundedObservation()
    {
        var validator = new MultichannelContentValidator();
        var analysis = CreateAnalysis(sideRms: 0.5, sideIndependence: 0);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        for (var index = 0; index < MultichannelContentValidator.MinimumActiveObservations; index++)
        {
            var elapsed = TimeSpan.FromTicks(
                MultichannelContentValidator.ObservationWindow.Ticks * index
                / (MultichannelContentValidator.MinimumActiveObservations - 1));
            validator.Observe(start + elapsed, analysis);
        }

        Assert.Equal(MultichannelValidationState.Uninformative, validator.State);
    }

    [Fact]
    public void SilenceDoesNotConsumeTheObservationWindow()
    {
        var validator = new MultichannelContentValidator();
        var silence = CreateAnalysis(sideRms: 0, sideIndependence: 0, frontRms: 0);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        validator.Observe(start, silence);
        validator.Observe(start.AddMinutes(1), silence);

        Assert.Equal(MultichannelValidationState.Pending, validator.State);
    }

    [Fact]
    public void ExplicitExpiryBoundsAProbeThatNeverReceivesActiveContent()
    {
        var validator = new MultichannelContentValidator();

        var state = validator.Expire();

        Assert.Equal(MultichannelValidationState.Uninformative, state);
        Assert.Equal(MultichannelValidationState.Uninformative, validator.State);
    }

    [Fact]
    public void ExpiryCannotDemoteAnAlreadyVerifiedLayout()
    {
        var validator = new MultichannelContentValidator();
        var useful = CreateAnalysis(sideRms: 0.5, sideIndependence: 1);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        for (var index = 0; index < MultichannelContentValidator.RequiredUsefulObservations; index++)
        {
            validator.Observe(start.AddMilliseconds(index * 100), useful);
        }

        var state = validator.Expire();

        Assert.Equal(MultichannelValidationState.Verified, state);
    }

    [Fact]
    public void RequiresSurroundEnergyToBeMaterialRelativeToTheWholeFrame()
    {
        var validator = new MultichannelContentValidator();
        var analysis = CreateAnalysis(sideRms: 0.001, sideIndependence: 1, frontRms: 1);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        for (var index = 0; index < MultichannelContentValidator.RequiredUsefulObservations; index++)
        {
            validator.Observe(start.AddMilliseconds(index * 100), analysis);
        }

        Assert.Equal(MultichannelValidationState.Pending, validator.State);
    }

    [Fact]
    public void ResetStartsANewValidationSession()
    {
        var validator = new MultichannelContentValidator();
        var useful = CreateAnalysis(sideRms: 0.5, sideIndependence: 1);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        for (var index = 0; index < MultichannelContentValidator.RequiredUsefulObservations; index++)
        {
            validator.Observe(start.AddMilliseconds(index * 100), useful);
        }

        validator.Reset();

        Assert.Equal(MultichannelValidationState.Pending, validator.State);
        Assert.Equal(MultichannelValidationState.Pending, validator.Observe(start.AddSeconds(1), useful));
    }

    private static MultichannelSignalAnalysis CreateAnalysis(
        double sideRms,
        double sideIndependence,
        double frontRms = 0.5)
    {
        var levels = new ChannelLevels(
            ChannelLayout.Surround51,
            [frontRms, frontRms, 0, 0, sideRms, 0]);
        return new MultichannelSignalAnalysis(
            levels,
            new Dictionary<SpeakerPosition, double>
            {
                [SpeakerPosition.SideLeft] = sideIndependence,
                [SpeakerPosition.SideRight] = 0
            });
    }
}
