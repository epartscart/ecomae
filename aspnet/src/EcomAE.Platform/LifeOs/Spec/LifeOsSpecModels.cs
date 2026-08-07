namespace EcomAE.Platform.LifeOs.Spec;

public sealed record LifeOsSpecPart(
    int Number,
    string Title,
    string Status,
    IReadOnlyList<string> Chapters,
    IReadOnlyList<string> Deliverables);

public sealed record LifeOsReasoningTrace(
    string TraceId,
    IReadOnlyList<string> Steps,
    double Confidence,
    IReadOnlyList<string> Assumptions,
    string Uncertainty);

public sealed record LifeOsDecisionRecord(
    string DecisionId,
    string Recommendation,
    double Confidence,
    IReadOnlyList<string> Sources,
    bool RequiresHumanApproval);

public sealed record LifeOsLearningSignal(
    string SignalId,
    string Kind,
    string Note,
    DateTimeOffset At);

public sealed record LifeOsPersonalityProfile(
    string Tone,
    string Formality,
    string Empathy,
    bool HumorEnabled,
    string Locale);

public sealed record LifeOsModalityAdapter(
    string Key,
    string Title,
    string Channel,
    string Status,
    IReadOnlyList<string> Capabilities);

public sealed record LifeOsApiSurface(
    string Path,
    string Method,
    string Purpose);

public sealed record LifeOsSecurityControl(
    string Id,
    string Domain,
    string Control,
    string Status);

public sealed record LifeOsClientSurface(
    string Key,
    string Title,
    string FormFactor,
    string Status);

public sealed record LifeOsPluginDescriptor(
    string Id,
    string Title,
    string Kind,
    string Status);

/// <summary>
/// Optional rich digests for Parts 2–10. When null for a part, <see cref="ILifeOsMasterSpec.FullDigest"/>
/// falls back to the registry stub for that part.
/// </summary>
public sealed record LifeOsSpecRuntimeDigests(
    object? Part2 = null,
    object? Part3 = null,
    object? Part4 = null,
    object? Part5 = null,
    object? Part6 = null,
    object? Part7 = null,
    object? Part8 = null,
    object? Part9 = null,
    object? Part10 = null);
