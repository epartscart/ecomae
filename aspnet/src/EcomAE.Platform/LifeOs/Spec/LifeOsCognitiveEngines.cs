using EcomAE.Platform.LifeOs.Part3;

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

    private static readonly string[] PersonalityModes =
    [
        "Professional", "Friendly", "Technical", "Teacher", "Coach",
        "Business Advisor", "Researcher", "Minimal", "Motivational", "Executive Assistant"
    ];

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

    public IReadOnlyList<LifeOsReasoningMethodResult> ReasonAll(
        string intent,
        LifeOsCurrentRealityModel reality,
        IReadOnlyList<string> entities)
    {
        var entityNote = entities.Count > 0 ? string.Join(",", entities) : "none";
        return
        [
            new(LifeOsReasoningMethod.Logical,
                $"IF calendar pressure AND focus={reality.FocusScore}",
                intent.Contains("meeting", StringComparison.OrdinalIgnoreCase)
                    ? "THEN prepare materials before the meeting"
                    : "THEN continue current activity with ambient assist",
                0.86),
            new(LifeOsReasoningMethod.Causal,
                "Incomplete prep → meeting quality drops → productivity decreases",
                "Suggest finishing the blocking task first",
                0.8),
            new(LifeOsReasoningMethod.Analogical,
                "Prior LifeOS / project sessions with similar CRM",
                "Reuse successful strategies from Experience Memory",
                0.72),
            new(LifeOsReasoningMethod.Probabilistic,
                $"Entities={entityNote}; interruptibility={reality.Interruptibility}",
                $"Recommended action confidence for '{intent}'",
                reality.FocusScore >= 80 ? 0.9 : 0.7),
        ];
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
        => LearnTyped(LifeOsLearningKind.Preference, outcome, feedback);

    public LifeOsLearningSignal LearnTyped(LifeOsLearningKind kind, string outcome, string feedback)
    {
        var signal = new LifeOsLearningSignal(
            $"LRN-{_signals.Count + 1:D4}",
            kind.ToString().ToLowerInvariant(),
            $"{outcome}: {feedback}",
            DateTimeOffset.UtcNow);
        _signals.Add(signal);
        return signal;
    }

    public LifeOsEmotionEstimate EstimateEmotion(LifeOsCurrentRealityModel reality, string intent)
    {
        string state;
        double conf;
        var signals = new List<string> { $"energy={reality.EnergyLevel}", $"focus={reality.FocusScore}" };

        if (reality.FocusScore >= 90 && reality.Interruptibility == "LOW")
        {
            state = "Focused";
            conf = 0.74;
        }
        else if (reality.EnergyLevel < 40)
        {
            state = "Tired";
            conf = 0.68;
            signals.Add("low-energy");
        }
        else if (intent.Contains("stress", StringComparison.OrdinalIgnoreCase))
        {
            state = "Stressed";
            conf = 0.55;
        }
        else
        {
            state = "Motivated";
            conf = 0.5;
        }

        return new LifeOsEmotionEstimate(state, conf, signals, UserMayOverride: true);
    }

    public string SelectPersonalityMode(LifeOsCurrentRealityModel reality, string intent)
    {
        if (intent.Contains("code", StringComparison.OrdinalIgnoreCase)
            || reality.Activity.Contains("Coding", StringComparison.OrdinalIgnoreCase))
        {
            return "Technical";
        }

        if (intent.Contains("learn", StringComparison.OrdinalIgnoreCase)
            || intent.Contains("explain", StringComparison.OrdinalIgnoreCase))
        {
            return "Teacher";
        }

        if (reality.UserState.Contains("Meeting", StringComparison.OrdinalIgnoreCase))
        {
            return "Executive Assistant";
        }

        if (reality.FocusScore >= 85)
        {
            return "Minimal";
        }

        return "Professional";
    }

    public object Digest() => new
    {
        part = 3,
        title = "AI & Cognitive Systems",
        engines = new[] { "Reasoning", "Decision", "Learning", "Personality", "Emotion" },
        personality = Personality,
        personalityModes = PersonalityModes,
        learningSignals = _signals.TakeLast(10).ToList(),
        status = "scaffold"
    };

    public object ReasoningDigest() => new
    {
        chapter = 16,
        methods = Enum.GetNames<LifeOsReasoningMethod>(),
        examples = new
        {
            logical = "IF meeting in 10m AND deck incomplete THEN finish deck first",
            causal = "Battery low → shutdown → meeting interrupted → suggest charger",
            analogical = "Compare to last year's project risks",
            probabilistic = "Restaurant recommendation confidence 94%"
        }
    };

    public object DecisionDigest() => new
    {
        chapter = 17,
        formula = "GoalPriority + ContextScore + Confidence + ResourceAvailability + TimeSensitivity + HistoricalSuccess - Risk",
        pipeline = new[]
        {
            "Problem", "Possible Actions", "Evaluate Context", "Risk Analysis",
            "Simulation", "Ranking", "Recommendation", "Execution", "Learning"
        }
    };

    public object LearningDigest() => new
    {
        chapter = 20,
        types = Enum.GetNames<LifeOsLearningKind>(),
        learns = new[]
        {
            "Writing style", "Coding preferences", "Speaking speed", "Favorite restaurants",
            "Workout habits", "Business interests", "Sleep patterns", "Travel preferences",
            "Notification preferences", "Communication style"
        },
        recent = _signals.TakeLast(8).ToList()
    };

    public object PersonalityDigest() => new
    {
        chapter = 21,
        modes = PersonalityModes,
        defaultProfile = Personality,
        note = "Users may assign different personalities to specialist agents"
    };

    public object EmotionDigest() => new
    {
        chapter = 22,
        inputs = new[]
        {
            "Voice characteristics", "Language patterns", "Facial expressions (optional)",
            "Interaction history", "Wearable indicators"
        },
        states = new[]
        {
            "Focused", "Relaxed", "Tired", "Frustrated", "Confused", "Motivated", "Stressed"
        },
        note = "Never guessed with certainty — confidence + user override always available"
    };
}
