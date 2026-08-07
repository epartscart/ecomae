namespace EcomAE.Platform.LifeOs.Part3;

/// <summary>Part 3 Ch.15 — Current Reality Model (CRM).</summary>
public sealed record LifeOsCurrentRealityModel(
    string UserState,
    string Activity,
    string Location,
    string Device,
    string? CalendarEvent,
    int EnergyLevel,
    int FocusScore,
    string Interruptibility,
    DateTimeOffset CapturedAt,
    IReadOnlyDictionary<string, double> SourceConfidence);

/// <summary>Part 3 Ch.14 — perception pipeline stage result.</summary>
public sealed record LifeOsPerceptionStage(string Name, string Output, double Confidence);

public sealed record LifeOsPerceptionResult(
    string PerceptionId,
    string Modality,
    IReadOnlyList<LifeOsPerceptionStage> Pipeline,
    string SemanticRepresentation,
    IReadOnlyList<string> ExtractedEntities);

public enum LifeOsReasoningMethod
{
    Logical,
    Causal,
    Analogical,
    Probabilistic
}

public sealed record LifeOsReasoningMethodResult(
    LifeOsReasoningMethod Method,
    string Premise,
    string Conclusion,
    double Confidence);

public sealed record LifeOsDecisionScore(
    double GoalPriority,
    double ContextScore,
    double Confidence,
    double Risk,
    double ResourceAvailability,
    double TimeSensitivity,
    double HistoricalSuccess,
    double Total)
{
    public static LifeOsDecisionScore Compute(
        double goal, double context, double confidence, double risk,
        double resources, double timeSensitivity, double historical)
    {
        // Risk subtracts; others add (scaffold weights equal).
        var total = goal + context + confidence + resources + timeSensitivity + historical - risk;
        return new(goal, context, confidence, risk, resources, timeSensitivity, historical, Math.Round(total, 3));
    }
}

public sealed record LifeOsPrediction(
    string PredictionId,
    string EventKind,
    double Probability,
    string Forecast,
    string Recommendation);

public enum LifeOsLearningKind
{
    Supervised,
    Reinforcement,
    Preference
}

public sealed record LifeOsEmotionEstimate(
    string State,
    double Confidence,
    IReadOnlyList<string> Signals,
    bool UserMayOverride);

public sealed record LifeOsEthicalCheck(
    string Name,
    bool Passed,
    string Detail);

public sealed record LifeOsEthicalVerdict(
    bool Allowed,
    IReadOnlyList<LifeOsEthicalCheck> Checks,
    string Summary);

public sealed record LifeOsReflectionReport(
    string ReportId,
    bool GoalAchieved,
    bool UserSatisfiedEstimate,
    bool Accurate,
    string EfficiencyNote,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> PreferenceUpdates);

/// <summary>Part 3 Ch.25 — one full unified cognitive cycle outcome.</summary>
public sealed record LifeOsCognitiveCycleResult(
    string CycleId,
    LifeOsPerceptionResult Perception,
    LifeOsCurrentRealityModel Reality,
    IReadOnlyList<LifeOsReasoningMethodResult> Reasoning,
    LifeOsDecisionScore DecisionScore,
    string Recommendation,
    LifeOsEthicalVerdict Ethics,
    IReadOnlyList<LifeOsPrediction> Predictions,
    LifeOsEmotionEstimate Emotion,
    string PersonalityMode,
    LifeOsReflectionReport? Reflection,
    IReadOnlyList<string> CycleStages,
    bool Executed);
