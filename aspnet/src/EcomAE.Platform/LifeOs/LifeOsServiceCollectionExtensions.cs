using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Orchestrator;

namespace EcomAE.Platform.LifeOs;

public static class LifeOsServiceCollectionExtensions
{
    /// <summary>Registers Part 2 LifeOS cognitive scaffold (in-memory bus + engines + orchestrator).</summary>
    public static IServiceCollection AddLifeOsPart2Scaffold(this IServiceCollection services)
    {
        services.AddSingleton<ILifeOsEventBus, InMemoryLifeOsEventBus>();
        services.AddSingleton<ILifeOsContextEngine, LifeOsContextEngine>();
        services.AddSingleton<ILifeOsMemorySystem, LifeOsMemorySystem>();
        services.AddSingleton<ILifeOsAgentFramework, LifeOsAgentFramework>();
        services.AddSingleton<ILifeOsPlanningEngine, LifeOsPlanningEngine>();
        services.AddSingleton<ILifeOsOrchestrator, LifeOsOrchestrator>();
        return services;
    }
}
