using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Spec;

namespace EcomAE.Platform.LifeOs.Part3;

public sealed class LifeOsAiCore : ILifeOsAiCore
{
    private readonly ILifeOsPerceptionEngine _perception;
    private readonly ILifeOsContextEngine _context;
    private readonly ILifeOsMemorySystem _memory;
    private readonly ILifeOsPlanningEngine _planning;
    private readonly ILifeOsPredictionEngine _prediction;
    private readonly ILifeOsEthicalAiLayer _ethics;
    private readonly ILifeOsSelfReflectionEngine _reflection;
    private readonly ILifeOsCognitiveEngines _cognitive;

    private static readonly string[] CycleStages =
    [
        "Perception", "Understanding", "Memory Retrieval", "Reasoning", "Planning",
        "Decision", "Agent Collaboration", "Execution", "Reflection", "Learning",
        "Memory Update", "Continuous Improvement"
    ];

    public LifeOsAiCore(
        ILifeOsPerceptionEngine perception,
        ILifeOsContextEngine context,
        ILifeOsMemorySystem memory,
        ILifeOsPlanningEngine planning,
        ILifeOsPredictionEngine prediction,
        ILifeOsEthicalAiLayer ethics,
        ILifeOsSelfReflectionEngine reflection,
        ILifeOsCognitiveEngines cognitive)
    {
        _perception = perception;
        _context = context;
        _memory = memory;
        _planning = planning;
        _prediction = prediction;
        _ethics = ethics;
        _reflection = reflection;
        _cognitive = cognitive;
    }

    public LifeOsCognitiveCycleResult RunCycle(LifeOsEvent input, bool userPermission = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        var cycleId = $"CYC-{input.EventId}";

        // Perception → Understanding
        var perception = _perception.Perceive(input);
        _memory.SeedDemoProject();
        var hints = _memory.Retrieve(query: "lifeos", take: 6);
        var ctx = _context.Build(input, hints);
        var reality = _context.BuildCurrentReality(input, ctx);

        var intent = input.Payload.GetValueOrDefault("transcript")
                     ?? perception.Pipeline.FirstOrDefault(p => p.Name == "Intent Recognition")?.Output
                     ?? input.Type.ToString();

        // Memory retrieval already via hints; store working
        _memory.Store(LifeOsMemoryLayer.Sensory, $"sense:{input.EventId}", perception.SemanticRepresentation);
        _memory.Store(LifeOsMemoryLayer.Working, $"crm:{cycleId}",
            $"{reality.UserState}/{reality.Activity}/{reality.Interruptibility}");

        // Reasoning (multi-method)
        var reasoning = _cognitive.ReasonAll(intent, reality, perception.ExtractedEntities);

        // Planning
        var plan = _planning.Decompose(intent);

        // Decision score (Ch.17 formula)
        var score = LifeOsDecisionScore.Compute(
            goal: intent.Contains("Launch", StringComparison.OrdinalIgnoreCase) ? 0.95 : 0.7,
            context: reality.FocusScore / 100.0,
            confidence: reasoning.Average(r => r.Confidence),
            risk: reality.Interruptibility == "LOW" ? 0.2 : 0.45,
            resources: 0.8,
            timeSensitivity: !string.IsNullOrWhiteSpace(reality.CalendarEvent) ? 0.85 : 0.4,
            historical: 0.65);

        var recommendation =
            $"Given CRM ({reality.Activity} @ {reality.Location}, focus={reality.FocusScore}), " +
            $"plan {plan.PlanId} ({plan.Tasks.Count} tasks). Top reasoning: {reasoning[0].Conclusion}";

        // Emotion + personality
        var emotion = _cognitive.EstimateEmotion(reality, intent);
        var personality = _cognitive.SelectPersonalityMode(reality, intent);

        // Ethics before execution
        var ethics = _ethics.Validate(recommendation, score.Confidence, userPermission, irreversible: false);
        var executed = ethics.Allowed && score.Total > 1.5;

        // Prediction
        var predictions = _prediction.Predict(reality, intent);

        // Reflection + learning
        LifeOsReflectionReport? reflection = null;
        if (executed || !ethics.Allowed)
        {
            reflection = _reflection.Reflect(intent, recommendation, ethics.Allowed, score.Total);
            _cognitive.LearnTyped(
                LifeOsLearningKind.Reinforcement,
                executed ? "accepted" : "blocked",
                reflection.EfficiencyNote);
            _memory.Store(LifeOsMemoryLayer.Experience, $"cycle:{cycleId}", recommendation);
        }

        return new LifeOsCognitiveCycleResult(
            cycleId,
            perception,
            reality,
            reasoning,
            score,
            recommendation,
            ethics,
            predictions,
            emotion,
            personality,
            reflection,
            CycleStages,
            executed);
    }

    public object ArchitectureDigest() => new
    {
        chapter = 13,
        title = "Cognitive Architecture",
        layers = new[]
        {
            new { name = "Perception Layer", items = new[] { "Voice", "Vision", "Screen", "Sensors", "Documents", "Location", "Calendar", "APIs" } },
            new { name = "Context Understanding Layer", items = new[] { "Intent", "Entity", "Activity", "Emotion", "Situation Awareness" } },
            new { name = "Cognitive Layer", items = new[] { "Memory", "Reasoning", "Decision", "Planning", "Learning", "Prediction" } },
            new { name = "Multi-Agent Layer", items = new[] { "Business", "Health", "Finance", "Coding", "Education", "Home", "Travel", "Automation", "Research" } },
            new { name = "Response Generation Layer", items = new[] { "Voice", "Text", "Workflow", "Automation", "Notifications", "API Calls" } }
        }
    };

    public object FullPart3Digest() => new
    {
        ok = true,
        part = 3,
        title = "Artificial Intelligence & Cognitive Architecture",
        chapters = Enumerable.Range(12, 14).Select(n => n).ToArray(), // 12..25
        chapterTitles = new Dictionary<int, string>
        {
            [12] = "Artificial Intelligence Core",
            [13] = "Cognitive Architecture",
            [14] = "Perception Engine",
            [15] = "Context Engine (CRM)",
            [16] = "Reasoning Engine",
            [17] = "Decision Engine",
            [18] = "Planning Engine",
            [19] = "Prediction Engine",
            [20] = "Learning Engine",
            [21] = "Personality Engine",
            [22] = "Emotion Engine",
            [23] = "Ethical AI Layer",
            [24] = "Self-Reflection Engine",
            [25] = "Unified Cognitive Cycle"
        },
        objectives = new[]
        {
            "Understand multimodal inputs",
            "Maintain persistent contextual awareness",
            "Plan and execute complex tasks",
            "Learn from user behavior",
            "Coordinate specialized AI agents",
            "Ensure ethical and safe operation",
            "Continuously optimize future decisions"
        },
        architecture = ArchitectureDigest(),
        perception = _perception.Digest(),
        contextCrm = _context.CrmDigest(),
        reasoning = _cognitive.ReasoningDigest(),
        decision = _cognitive.DecisionDigest(),
        planning = _planning.PlannerTypesDigest(),
        prediction = _prediction.Digest(),
        learning = _cognitive.LearningDigest(),
        personality = _cognitive.PersonalityDigest(),
        emotion = _cognitive.EmotionDigest(),
        ethics = _ethics.Digest(),
        reflection = _reflection.Digest(),
        cognitiveCycle = CycleStages,
        status = "scaffold"
    };
}
