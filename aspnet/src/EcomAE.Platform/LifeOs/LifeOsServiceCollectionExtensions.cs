using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Spec;

namespace EcomAE.Platform.LifeOs;

public static class LifeOsServiceCollectionExtensions
{
    /// <summary>Registers LifeOS Parts 2–10 scaffold (bus, engines, orchestrator, master spec).</summary>
    public static IServiceCollection AddLifeOsPart2Scaffold(this IServiceCollection services)
    {
        services.AddSingleton<ILifeOsEventBus, InMemoryLifeOsEventBus>();
        services.AddSingleton<ILifeOsContextEngine, LifeOsContextEngine>();
        services.AddSingleton<ILifeOsMemorySystem, LifeOsMemorySystem>();
        services.AddSingleton<ILifeOsAgentFramework, LifeOsAgentFramework>();
        services.AddSingleton<ILifeOsPlanningEngine, LifeOsPlanningEngine>();
        services.AddSingleton<ILifeOsOrchestrator, LifeOsOrchestrator>();
        services.AddSingleton<ILifeOsCognitiveEngines, LifeOsCognitiveEngines>();
        services.AddSingleton<ILifeOsMasterSpec, LifeOsMasterSpec>();
        return services;
    }
}
