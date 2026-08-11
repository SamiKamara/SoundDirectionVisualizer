namespace SoundDirectionVisualizer.Core.Audio;

public enum MultichannelValidationState
{
    Pending,
    Verified,
    Uninformative
}

public sealed class MultichannelContentValidator
{
    public const int MinimumActiveObservations = 32;
    public const int RequiredUsefulObservations = 3;
    public const double MinimumSurroundEnergyShare = 0.02;
    public const double MinimumIndependentSurroundEnergyShare = 0.01;
    public static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan MaximumProbeDuration = TimeSpan.FromSeconds(12);

    private DateTimeOffset? _firstActiveTimestamp;
    private int _activeObservations;
    private int _usefulObservations;

    public MultichannelValidationState State { get; private set; } = MultichannelValidationState.Pending;

    public MultichannelValidationState Observe(
        DateTimeOffset timestamp,
        MultichannelSignalAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        if (State != MultichannelValidationState.Pending)
        {
            return State;
        }

        double totalDirectionalEnergy = 0;
        double surroundEnergy = 0;
        double independentSurroundEnergy = 0;

        for (var index = 0; index < analysis.Levels.Layout.ChannelCount; index++)
        {
            var position = analysis.Levels.Layout.Positions[index];
            if (position == SpeakerPosition.LowFrequency)
            {
                continue;
            }

            var rms = analysis.Levels.RmsLevels[index];
            var energy = rms * rms;
            totalDirectionalEnergy += energy;

            if (MultichannelSignalAnalyzer.IsSurroundPosition(position))
            {
                surroundEnergy += energy;
                independentSurroundEnergy += energy * analysis.GetIndependenceRatio(position);
            }
        }

        if (totalDirectionalEnergy <= 1e-12)
        {
            return State;
        }

        _firstActiveTimestamp ??= timestamp;
        _activeObservations++;

        if (surroundEnergy / totalDirectionalEnergy >= MinimumSurroundEnergyShare
            && independentSurroundEnergy / totalDirectionalEnergy >= MinimumIndependentSurroundEnergyShare)
        {
            _usefulObservations++;
            if (_usefulObservations >= RequiredUsefulObservations)
            {
                State = MultichannelValidationState.Verified;
                return State;
            }
        }

        if (_activeObservations >= MinimumActiveObservations
            && timestamp - _firstActiveTimestamp.Value >= ObservationWindow)
        {
            State = MultichannelValidationState.Uninformative;
        }

        return State;
    }

    public void Reset()
    {
        _firstActiveTimestamp = null;
        _activeObservations = 0;
        _usefulObservations = 0;
        State = MultichannelValidationState.Pending;
    }

    public MultichannelValidationState Expire()
    {
        if (State == MultichannelValidationState.Pending)
        {
            State = MultichannelValidationState.Uninformative;
        }

        return State;
    }
}
