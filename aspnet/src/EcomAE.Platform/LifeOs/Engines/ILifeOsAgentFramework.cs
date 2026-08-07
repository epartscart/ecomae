using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

/// <summary>Part 2 Ch.10 — specialist agent network coordinated by the Orchestrator.</summary>
public interface ILifeOsAgentFramework
{
    IReadOnlyList<LifeOsAgentDescriptor> Catalog { get; }

    IReadOnlyList<string> SelectAgents(string intent, LifeOsContextObject context);

    Task<LifeOsAgentResult> InvokeAsync(
        string agentKey,
        LifeOsEvent trigger,
        LifeOsContextObject context,
        CancellationToken cancellationToken = default);
}
