namespace EcomAE.Platform.LifeOs.Part9;

/// <summary>
/// Part 9 — Ecosystem Platform, Marketplace &amp; Developer Platform (Ch.126–150).
/// Marketplace / Agent Store / Developer Portal scaffold (not a live commerce engine).
/// </summary>
public interface ILifeOsEcosystemPlatform
{
    IReadOnlyList<LifeOsEcosystemAnalog> EcosystemAnalogs { get; }

    IReadOnlyList<string> PlatformFoundationBlocks { get; }

    IReadOnlyList<LifeOsPlatformLayer> PlatformLayers { get; }

    IReadOnlyList<LifeOsMarketplaceStore> MarketplaceStores { get; }

    IReadOnlyList<string> MarketplaceCatalogKinds { get; }

    IReadOnlyList<LifeOsAgentCategory> AgentCategories { get; }

    LifeOsAgentListing SampleAgentListing { get; }

    IReadOnlyList<LifeOsPluginExample> PluginExamples { get; }

    IReadOnlyList<string> PluginLifecycle { get; }

    IReadOnlyList<LifeOsAppExample> ApplicationExamples { get; }

    IReadOnlyList<string> AppComponentStack { get; }

    IReadOnlyList<LifeOsWorkflowTemplate> WorkflowTemplates { get; }

    IReadOnlyList<LifeOsKnowledgePack> KnowledgePacks { get; }

    IReadOnlyList<LifeOsIntegrationCategory> IntegrationCategories { get; }

    IReadOnlyList<string> IntegrationFlow { get; }

    IReadOnlyList<string> DeveloperPortalModules { get; }

    IReadOnlyList<LifeOsSdkLanguage> SdkLanguages { get; }

    IReadOnlyList<LifeOsSdkModule> SdkModules { get; }

    IReadOnlyList<LifeOsPublicApi> PublicApis { get; }

    IReadOnlyList<LifeOsCliCommand> CliCommands { get; }

    IReadOnlyList<LifeOsBillingPlan> BillingPlans { get; }

    IReadOnlyList<LifeOsUsageMetric> UsageMetrics { get; }

    IReadOnlyList<LifeOsLicenseType> LicenseTypes { get; }

    IReadOnlyList<string> RevenueModels { get; }

    IReadOnlyList<string> CertificationChecks { get; }

    IReadOnlyList<LifeOsCertificationLevel> CertificationLevels { get; }

    IReadOnlyList<LifeOsPartnerKind> PartnerKinds { get; }

    IReadOnlyList<LifeOsPartnerProgram> PartnerPrograms { get; }

    IReadOnlyList<LifeOsCommunityFeature> CommunityFeatures { get; }

    IReadOnlyList<LifeOsAiModelCategory> AiModelCategories { get; }

    IReadOnlyList<string> CommerceFeatures { get; }

    IReadOnlyList<string> GovernanceAreas { get; }

    IReadOnlyList<string> ReviewProcess { get; }

    IReadOnlyList<string> EcosystemAnalytics { get; }

    IReadOnlyList<LifeOsRoadmapPhase> EcosystemRoadmap { get; }

    object MarketplaceDigest();

    object AgentAndPluginDigest();

    object AppWorkflowKnowledgeDigest();

    object DeveloperPlatformDigest();

    object BillingLicensingDigest();

    object PartnersCommunityGovernanceDigest();

    object RoadmapAndAnalyticsDigest();

    object FullPart9Digest();
}
