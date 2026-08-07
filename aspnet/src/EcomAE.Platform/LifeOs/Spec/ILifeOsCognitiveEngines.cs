using EcomAE.Platform.LifeOs.Part3;

namespace EcomAE.Platform.LifeOs.Spec;

/// <summary>Part 3 — Reasoning, Decision, Learning, Personality, Emotion engines.</summary>
public interface ILifeOsCognitiveEngines
{
    LifeOsReasoningTrace Reason(string intent, IReadOnlyList<string> evidence);

    IReadOnlyList<LifeOsReasoningMethodResult> ReasonAll(
        string intent,
        LifeOsCurrentRealityModel reality,
        IReadOnlyList<string> entities);

    LifeOsDecisionRecord Decide(LifeOsReasoningTrace trace, bool allowIrreversible);

    LifeOsLearningSignal Learn(string outcome, string feedback);

    LifeOsLearningSignal LearnTyped(LifeOsLearningKind kind, string outcome, string feedback);

    LifeOsEmotionEstimate EstimateEmotion(LifeOsCurrentRealityModel reality, string intent);

    string SelectPersonalityMode(LifeOsCurrentRealityModel reality, string intent);

    LifeOsPersonalityProfile Personality { get; }

    object Digest();

    object ReasoningDigest();

    object DecisionDigest();

    object LearningDigest();

    object PersonalityDigest();

    object EmotionDigest();
}
