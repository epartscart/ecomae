namespace EcomAE.Platform.Services;

/// <summary>
/// ecomae Intelligence Platform ecosystem registry — BOS modules + ambient OS products.
/// Scaffold catalog for IP hub and LifeOS marketing; runtime agents land in later waves.
/// </summary>
public static class EcomaeEcosystemCatalog
{
    public sealed record EcosystemNode(
        string Key,
        string Title,
        string Tagline,
        string Href,
        string Status,
        IReadOnlyList<string>? Children = null);

    /// <summary>BOS Business Operating System modules under the Intelligence Platform.</summary>
    public static readonly IReadOnlyList<EcosystemNode> BosModules =
    [
        new("erp", "ERP", "Finance, inventory, production, compliance", "/erp", "live"),
        new("crm", "CRM", "Accounts, pipeline, customer intelligence", "/cp", "live"),
        new("workflow", "Workflow", "Approvals, routing, operational flow", "/bos/app", "scaffold"),
        new("automation", "Automation", "Rules, jobs, and hands-free ops", "/bos/app", "scaffold"),
        new("ai", "AI", "Price engine, CRM assist, automation agents", "/ip/app", "scaffold"),
        new("cp", "Control Panel", "Tenant fleet and platform ops", "/cp", "live"),
    ];

    /// <summary>Ambient OS products delivered to customers via the Intelligence Platform.</summary>
    public static readonly IReadOnlyList<EcosystemNode> AmbientOsProducts =
    [
        new(
            "lifeos",
            "LifeOS™",
            "Universal Ambient Artificial Intelligence Operating System",
            "https://lifeos.ecomae.com/",
            "live-scaffold",
            [
                "Context Engine",
                "Memory Engine",
                "Planning Engine",
                "Decision Engine",
                "Reasoning Engine",
                "Learning Engine",
                "Safety Engine",
                "Emotion & Personality Engine",
                "Agent Orchestrator",
            ]),
        new("healthos", "HealthOS", "Ambient health intelligence & care coordination", "#", "coming-soon"),
        new("eduos", "EduOS", "Lifelong learning and tutor agents", "#", "coming-soon"),
        new("homeos", "HomeOS", "Home, energy, and ambient living", "#", "coming-soon"),
        new("retailos", "RetailOS", "Commerce and retail ambient ops", "#", "coming-soon"),
        new("industryos", "IndustryOS", "Industrial and field intelligence", "#", "coming-soon"),
        new("cityos", "CityOS", "Civic and urban ambient systems", "#", "coming-soon"),
        new("future", "Future Products", "Next ambient OS products on the IP fabric", "#", "roadmap"),
    ];

    /// <summary>Multi-client / tenant management surfaces under IP desktop.</summary>
    public static readonly IReadOnlyList<EcosystemNode> ClientManagement =
    [
        new("tenants", "Tenants", "Fleet of client sites and portals", "/cp/tenants-app", "live"),
        new("tenant-features", "Tenant features", "Per-client feature flags", "/cp/tenant-features-app", "live"),
        new("customer-board", "Customer board", "Multi-client account board", "/cp/customer-board-app", "live"),
        new("fleet-summary", "BOS fleet", "Platform fleet health summary", "/bos/fleet-summary-app", "live"),
        new("users", "Users & groups", "Operator and client users", "/cp/users-app", "live"),
        new("api-clients", "API clients", "Integration client credentials", "/cp/api-clients-app", "live"),
    ];

    /// <summary>LifeOS console apps launched from the IP desktop.</summary>
    public static readonly IReadOnlyList<EcosystemNode> LifeOsConsoles =
    [
        new("home", "LifeOS Home", "Premium product home", "/lifeos", "live"),
        new("brain", "Brain console", "Nine cognitive engines", "/lifeos/brain", "scaffold"),
        new("architecture", "Architecture", "Part 2 orchestrator console", "/lifeos/architecture-app", "scaffold"),
        new("cognitive", "Cognitive", "Part 3 AI core", "/lifeos/cognitive-app", "scaffold"),
        new("multimodal", "Multimodal", "Part 4 runtime", "/lifeos/multimodal-app", "scaffold"),
        new("platform", "Platform", "Part 5 engineering", "/lifeos/platform-app", "scaffold"),
        new("infra", "Infra / SRE", "Part 6 cloud ops", "/lifeos/infra-app", "scaffold"),
        new("security", "Security", "Part 7 governance", "/lifeos/security-app", "scaffold"),
        new("clients", "Client UX", "Part 8 experience", "/lifeos/clients-app", "scaffold"),
        new("plugins", "Ecosystem", "Part 9 marketplace", "/lifeos/plugins-app", "scaffold"),
        new("roadmap", "Part 10 Roadmap", "Execution strategy Ch.151–168", "/lifeos/roadmap-app", "scaffold"),
        new("spec", "Master Spec", "Parts 1–10 registry", "/lifeos/spec-app", "scaffold"),
        new("guide", "CP system guide", "Full visual + link catalog", "/cp/lifeos-guide-app", "live"),
    ];

    public static EcosystemNode? FindOs(string key) =>
        AmbientOsProducts.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}
