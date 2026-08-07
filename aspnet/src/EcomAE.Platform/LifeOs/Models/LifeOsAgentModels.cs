namespace EcomAE.Platform.LifeOs.Models;

/// <summary>Part 2 Ch.10 — specialist agent registry entry.</summary>
public sealed record LifeOsAgentDescriptor(
    string Key,
    string Title,
    string Domain,
    IReadOnlyList<string> Capabilities,
    string Status);

public enum LifeOsAgentLifecycleStage
{
    Request,
    Context,
    CapabilityCheck,
    MemoryRetrieval,
    Execution,
    Validation,
    Result,
    Learning
}

public sealed record LifeOsAgentInvocation(
    string AgentKey,
    LifeOsAgentLifecycleStage Stage,
    string Note,
    double Confidence);

public sealed record LifeOsAgentResult(
    string AgentKey,
    bool Ok,
    string Summary,
    double Confidence,
    IReadOnlyList<LifeOsAgentLifecycleStage> StagesCompleted);
