using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Orchestrator;

/// <summary>
/// Part 2 Ch.6 — central intelligence. Every event flows through the Orchestrator:
/// permissions → context → intent → priority → agents → aggregation → learning feedback.
/// </summary>
public interface ILifeOsOrchestrator
{
    Task<LifeOsOrchestrationResult> ProcessAsync(
        LifeOsEvent input,
        CancellationToken cancellationToken = default);

    object ArchitectureDigest();
}
