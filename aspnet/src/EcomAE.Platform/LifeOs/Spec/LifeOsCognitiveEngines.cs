namespace EcomAE.Platform.LifeOs.Spec;

public sealed class LifeOsCognitiveEngines : ILifeOsCognitiveEngines
{
    private readonly List<LifeOsLearningSignal> _signals = [];

    public LifeOsPersonalityProfile Personality { get; } = new(
        Tone: "calm-companion",
        Formality: "adaptive",
        Empathy: "high",
        HumorEnabled: false,
        Locale: "en");

    public LifeOsReasoningTrace Reason(string intent, IReadOnlyList<string> evidence)
    {
        var steps = new List<string>
        {
            $"Interpret intent: {intent}",
            "Retrieve relevant memory & context scores",
            "Enumerate candidate agent proposals",
            "Score options by confidence, risk, and user policy",
            "Produce explainable recommendation"
        };
        if (evidence.Count > 0)
        {
            steps.Insert(1, $"Evidence: {string.Join("; ", evidence.Take(4))}");
        }

        return new LifeOsReasoningTrace(
            $"RSN-{DateTimeOffset.UtcNow:HHmmssfff}",
            steps,
            0.78,
            ["User retains approval for irreversible actions", "Local-first privacy preferred"],
            "Moderate — multimodal sensors not live in scaffold");
    }

    public LifeOsDecisionRecord Decide(LifeOsReasoningTrace trace, bool allowIrreversible)
    {
        var needsApproval = !allowIrreversible;
        return new LifeOsDecisionRecord(
            $"DEC-{trace.TraceId}",
            needsApproval
                ? "Propose action and await human confirmation"
                : "Execute low-risk automation within policy",
            trace.Confidence,
            trace.Steps.Take(3).ToList(),
            needsApproval);
    }

    public LifeOsLearningSignal Learn(string outcome, string feedback)
    {
        var signal = new LifeOsLearningSignal(
            $"LRN-{_signals.Count + 1:D4}",
            "preference",
            $"{outcome}: {feedback}",
            DateTimeOffset.UtcNow);
        _signals.Add(signal);
        return signal;
    }

    public object Digest() => new
    {
        part = 3,
        title = "AI & Cognitive Systems",
        engines = new[] { "Reasoning", "Decision", "Learning", "Personality" },
        personality = Personality,
        learningSignals = _signals.TakeLast(10).ToList(),
        status = "scaffold"
    };
}
