using System.Collections.ObjectModel;

namespace SoundDirectionVisualizer.Core.Audio;

public sealed class MultichannelSignalAnalysis
{
    private readonly ReadOnlyDictionary<SpeakerPosition, double> _independenceRatios;

    public MultichannelSignalAnalysis(
        ChannelLevels levels,
        IReadOnlyDictionary<SpeakerPosition, double> independenceRatios)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(independenceRatios);

        Levels = levels;
        StereoFallbackLevels = levels.ToStereoFallback();
        _independenceRatios = new ReadOnlyDictionary<SpeakerPosition, double>(
            new Dictionary<SpeakerPosition, double>(independenceRatios));
    }

    public ChannelLevels Levels { get; }

    public StereoLevels StereoFallbackLevels { get; }

    public IReadOnlyDictionary<SpeakerPosition, double> IndependenceRatios => _independenceRatios;

    public double GetIndependenceRatio(SpeakerPosition position) =>
        _independenceRatios.TryGetValue(position, out var ratio) ? ratio : 0;
}
