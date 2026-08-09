using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Direction;

public sealed record DirectionFrame(
    DateTimeOffset Timestamp,
    StereoLevels Levels,
    DirectionEstimate Estimate);
