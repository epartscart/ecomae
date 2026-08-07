namespace EcomAE.Platform.LifeOs.Spec;

/// <summary>Part 3 — Reasoning, Decision, Learning, Personality engines (scaffold).</summary>
public interface ILifeOsCognitiveEngines
{
    LifeOsReasoningTrace Reason(string intent, IReadOnlyList<string> evidence);

    LifeOsDecisionRecord Decide(LifeOsReasoningTrace trace, bool allowIrreversible);

    LifeOsLearningSignal Learn(string outcome, string feedback);

    LifeOsPersonalityProfile Personality { get; }

    object Digest();
}
