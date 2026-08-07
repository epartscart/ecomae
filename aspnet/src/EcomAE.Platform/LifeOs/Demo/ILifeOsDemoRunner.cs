namespace EcomAE.Platform.LifeOs.Demo;

/// <summary>
/// Sample-data demo runner — shows how LifeOS Perceive→Decide→Act→Learn works (scaffold dry-run).
/// </summary>
public interface ILifeOsDemoRunner
{
    IReadOnlyList<LifeOsDemoScenario> Scenarios { get; }

    LifeOsDemoScenario DefaultScenario { get; }

    Task<LifeOsDemoRunResult> RunAsync(string? scenarioKey = null, string? transcriptOverride = null, bool confirm = false, CancellationToken cancellationToken = default);

    object CatalogDigest();
}
