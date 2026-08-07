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

    public static EcosystemNode? FindOs(string key) =>
        AmbientOsProducts.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}
