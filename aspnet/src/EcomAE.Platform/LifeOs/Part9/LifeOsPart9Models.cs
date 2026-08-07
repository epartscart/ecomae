namespace EcomAE.Platform.LifeOs.Part9;

public sealed record LifeOsEcosystemAnalog(string Key, string Title, string Domain);

public sealed record LifeOsPlatformLayer(int Number, string Title, string Notes);

public sealed record LifeOsMarketplaceStore(string Key, string Title);

public sealed record LifeOsAgentCategory(string Key, string Title);

public sealed record LifeOsAgentListing(
    string AgentId,
    string Name,
    string Publisher,
    string Category,
    double Rating,
    long Downloads,
    IReadOnlyList<string> Permissions);

public sealed record LifeOsPluginExample(string Key, string Title, string Category);

public sealed record LifeOsAppExample(string Key, string Title);

public sealed record LifeOsWorkflowTemplate(string Key, string Title);

public sealed record LifeOsKnowledgePack(string Key, string Title, string Kind);

public sealed record LifeOsIntegrationCategory(string Key, string Title);

public sealed record LifeOsSdkLanguage(string Key, string Title);

public sealed record LifeOsSdkModule(string Key, string Title);

public sealed record LifeOsPublicApi(string Method, string Path, string Purpose);

public sealed record LifeOsCliCommand(string Command, string Purpose);

public sealed record LifeOsBillingPlan(string Key, string Title);

public sealed record LifeOsUsageMetric(string Key, string Title);

public sealed record LifeOsLicenseType(string Key, string Title);

public sealed record LifeOsCertificationLevel(string Key, string Title);

public sealed record LifeOsPartnerKind(string Key, string Title);

public sealed record LifeOsPartnerProgram(string Key, string Title);

public sealed record LifeOsCommunityFeature(string Key, string Title);

public sealed record LifeOsAiModelCategory(string Key, string Title);

public sealed record LifeOsRoadmapPhase(
    string Key,
    string Title,
    IReadOnlyList<string> Deliverables);
