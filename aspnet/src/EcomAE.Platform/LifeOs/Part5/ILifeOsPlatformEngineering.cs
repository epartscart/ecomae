namespace EcomAE.Platform.LifeOs.Part5;

/// <summary>
/// Part 5 — Platform Engineering & Developer Architecture (Ch.42–60).
/// Cloud-native, event-driven, API-first scaffold registry.
/// </summary>
public interface ILifeOsPlatformEngineering
{
    IReadOnlyList<string> EngineeringPrinciples { get; }

    IReadOnlyList<LifeOsMicroservice> Microservices { get; }

    IReadOnlyList<LifeOsApiConvention> RestConventions { get; }

    IReadOnlyList<string> WebSocketChannels { get; }

    IReadOnlyList<LifeOsEventTopic> EventTopics { get; }

    IReadOnlyList<LifeOsDataStore> DataStores { get; }

    IReadOnlyList<string> MemoryLayers { get; }

    IReadOnlyList<LifeOsPluginManifest> SamplePlugins { get; }

    IReadOnlyList<LifeOsAgentSdkContract> AgentSdkContract { get; }

    IReadOnlyList<LifeOsAiRoute> AiGatewayRoutes { get; }

    LifeOsApiEnvelope Ok(object data, object? meta = null);

    LifeOsApiEnvelope Fail(string code, string message);

    object KnowledgeGraphSample();

    object WorkflowDigest();

    object AutomationDigest();

    object AuthDigest();

    object MultiTenantDigest();

    object ObservabilityDigest();

    object EngineeringStandardsDigest();

    object FullPart5Digest();
}
