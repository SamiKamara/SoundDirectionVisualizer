using SoundDirectionVisualizer.Core.Audio;

namespace SoundDirectionVisualizer.Core.Tests;

public sealed class AdaptiveLoudnessClassifierTests
{
    [Fact]
    public void LevelAboveRecentAmbientMultiplierIsClassifiedAsLoud()
    {
        var classifier = CreateTrainedClassifier();

        var result = classifier.Update(
            new StereoLevels(0.40, 0.40),
            silenceRmsThreshold: 0.001,
            loudnessMultiplier: 2.5);

        Assert.Equal(SoundLoudness.Loud, result);
    }

    [Fact]
    public void LevelBelowRecentAmbientMultiplierRemainsAmbient()
    {
        var classifier = CreateTrainedClassifier();

        var result = classifier.Update(
            new StereoLevels(0.20, 0.20),
            silenceRmsThreshold: 0.001,
            loudnessMultiplier: 2.5);

        Assert.Equal(SoundLoudness.Ambient, result);
    }

    [Fact]
    public void ClassifierDoesNotGuessLoudnessBeforeItHasAnAmbientBaseline()
    {
        var classifier = new AdaptiveLoudnessClassifier();

        var result = classifier.Update(
            new StereoLevels(0.80, 0.80),
            silenceRmsThreshold: 0.001,
            loudnessMultiplier: 2.5);

        Assert.Equal(SoundLoudness.Ambient, result);
    }

    [Fact]
    public void SustainedLevelBecomesTheNewAmbientBaseline()
    {
        var classifier = CreateTrainedClassifier();
        SoundLoudness result = default;

        for (var index = 0; index < 300; index++)
        {
            result = classifier.Update(
                new StereoLevels(0.40, 0.40),
                silenceRmsThreshold: 0.001,
                loudnessMultiplier: 2.5);
        }

        Assert.Equal(SoundLoudness.Ambient, result);
    }

    [Fact]
    public void ResetDiscardsThePreviousAmbientBaseline()
    {
        var classifier = CreateTrainedClassifier();

        classifier.Reset();
        var result = classifier.Update(
            new StereoLevels(0.40, 0.40),
            silenceRmsThreshold: 0.001,
            loudnessMultiplier: 2.5);

        Assert.Equal(SoundLoudness.Ambient, result);
    }

    private static AdaptiveLoudnessClassifier CreateTrainedClassifier()
    {
        var classifier = new AdaptiveLoudnessClassifier();
        for (var index = 0; index < 64; index++)
        {
            classifier.Update(
                new StereoLevels(0.10, 0.10),
                silenceRmsThreshold: 0.001,
                loudnessMultiplier: 2.5);
        }

        return classifier;
    }
}
