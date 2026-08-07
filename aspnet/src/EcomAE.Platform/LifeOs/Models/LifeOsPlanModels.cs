namespace EcomAE.Platform.LifeOs.Models;

/// <summary>Part 2 Ch.11 — goal decomposition into executable workflow tasks.</summary>
public enum LifeOsTaskStatus
{
    Pending,
    Ready,
    InProgress,
    Blocked,
    Done
}

public sealed record LifeOsPlanTask(
    string TaskId,
    string Title,
    int Priority,
    IReadOnlyList<string> DependsOn,
    LifeOsTaskStatus Status);

public sealed record LifeOsPlan(
    string PlanId,
    string Goal,
    DateTimeOffset CreatedAt,
    IReadOnlyList<LifeOsPlanTask> Tasks);

public sealed record LifeOsOrchestrationResult(
    string TraceId,
    LifeOsEvent Input,
    LifeOsContextObject Context,
    string Intent,
    IReadOnlyList<string> SelectedAgents,
    IReadOnlyList<LifeOsAgentResult> AgentResults,
    LifeOsPlan? Plan,
    string AggregatedResponse,
    string RiskLevel,
    bool PermissionOk,
    IReadOnlyList<string> Pipeline);
