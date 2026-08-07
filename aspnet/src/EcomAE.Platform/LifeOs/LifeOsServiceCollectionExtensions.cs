using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Part3;
using EcomAE.Platform.LifeOs.Part4;
using EcomAE.Platform.LifeOs.Part5;
using EcomAE.Platform.LifeOs.Part6;
using EcomAE.Platform.LifeOs.Spec;

namespace EcomAE.Platform.LifeOs;

public static class LifeOsServiceCollectionExtensions
{
    /// <summary>Registers LifeOS Parts 2–10 scaffold (bus, engines, AI Core, multimodal, platform, cloud ops, master spec).</summary>
    public static IServiceCollection AddLifeOsPart2Scaffold(this IServiceCollection services)
    {
        services.AddSingleton<ILifeOsEventBus, InMemoryLifeOsEventBus>();
        services.AddSingleton<ILifeOsContextEngine, LifeOsContextEngine>();
        services.AddSingleton<ILifeOsMemorySystem, LifeOsMemorySystem>();
        services.AddSingleton<ILifeOsAgentFramework, LifeOsAgentFramework>();
        services.AddSingleton<ILifeOsPlanningEngine, LifeOsPlanningEngine>();
        services.AddSingleton<ILifeOsOrchestrator, LifeOsOrchestrator>();
        services.AddSingleton<ILifeOsCognitiveEngines, LifeOsCognitiveEngines>();
        services.AddSingleton<ILifeOsPerceptionEngine, LifeOsPerceptionEngine>();
        services.AddSingleton<ILifeOsPredictionEngine, LifeOsPredictionEngine>();
        services.AddSingleton<ILifeOsEthicalAiLayer, LifeOsEthicalAiLayer>();
        services.AddSingleton<ILifeOsSelfReflectionEngine, LifeOsSelfReflectionEngine>();
        services.AddSingleton<ILifeOsAiCore, LifeOsAiCore>();
        services.AddSingleton<ILifeOsMultimodalRuntime, LifeOsMultimodalRuntime>();
        services.AddSingleton<ILifeOsPlatformEngineering, LifeOsPlatformEngineering>();
        services.AddSingleton<ILifeOsCloudOperations, LifeOsCloudOperations>();
        services.AddSingleton<ILifeOsMasterSpec, LifeOsMasterSpec>();
        return services;
    }
}
