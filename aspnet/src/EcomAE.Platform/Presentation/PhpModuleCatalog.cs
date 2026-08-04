namespace EcomAE.Platform.Presentation;

/// <summary>
/// Helpers over the generated PHP module directory.
/// Interactive module bodies remain on PHP; ASP.NET chrome lists every surface so none are omitted.
/// </summary>
public static partial class PhpModuleCatalog
{
    public static int TotalTrackedCount =>
        ErpCategoryCount + ErpAreaCount + ErpTabCount + BosModuleCount + CpBrochureFeatureCount + StorefrontSurfaceCount;

    public static IEnumerable<ModuleLink> AllTrackedLinks()
        => ErpCategories
            .Concat(ErpAreas)
            .Concat(ErpTabs)
            .Concat(BosModules)
            .Concat(CpBrochureFeatures)
            .Concat(StorefrontSurfaces);

    public static IReadOnlyDictionary<string, object> BuildSummary() => new Dictionary<string, object>
    {
        ["policy"] = "hybrid-deeplink-to-php-until-aspnet-module-complete",
        ["erpCategories"] = ErpCategoryCount,
        ["erpAreas"] = ErpAreaCount,
        ["erpTabs"] = ErpTabCount,
        ["bosModules"] = BosModuleCount,
        ["cpBrochureFeatures"] = CpBrochureFeatureCount,
        ["storefrontSurfaces"] = StorefrontSurfaceCount,
        ["totalTracked"] = TotalTrackedCount,
        ["directoryCoverage"] = new Dictionary<string, object>
        {
            ["cpCommandCentre"] = "CpBrochureFeatures",
            ["erpDashboard"] = "ErpCategories+ErpAreas+ErpTabs",
            ["bosFleet"] = "BosModules",
            ["storefrontPreview"] = "StorefrontSurfaces",
            ["omittedKinds"] = Array.Empty<string>(),
            ["fullCatalogFloor"] = 714,
        },
        ["aspNetInteractiveComplete"] = 0,
        ["cutoverAllowed"] = false,
        ["readyForPhpRemoval"] = false,
        ["deeplinkFloorOk"] = AllTrackedLinks().All(link => IsAllowedPhpDeeplink(link.Href)),
        ["notes"] = new[]
        {
            "Live product chrome remains PHP (/CP/ /ERP/ /BOS/ storefront hosts).",
            "ASP.NET /cp|/erp|/bos|/storefront/app shells expose this full directory via hybrid deeplinks.",
            "ERP shells list categories + areas + tabs; CP lists all brochure features; BOS all modules; storefront all surfaces.",
            "Tenant hosts must not receive presentation shadows (see TENANT_MIGRATION_SAFETY.md)."
        }
    };

    public static IEnumerable<IGrouping<string, ModuleLink>> CpFeaturesByCategory()
        => CpBrochureFeatures.GroupBy(x => x.Group ?? "general");

    public static IEnumerable<IGrouping<string, ModuleLink>> ErpTabsByArea()
        => ErpTabs.GroupBy(x => x.Group ?? "overview");

    /// <summary>Hybrid workspace URL that keeps ASP.NET chrome and loads PHP module in iframe.</summary>
    public static string HybridWorkspaceHref(string surfaceAppPath, string phpHref)
        => $"{surfaceAppPath}?php={Uri.EscapeDataString(phpHref)}";

    /// <summary>
    /// Allowed hybrid iframe targets: PHP CP/ERP/BOS paths or storefront absolute/relative URLs.
    /// Rejects javascript:/data:/aspnet app routes masquerading as PHP modules.
    /// </summary>
    public static bool IsAllowedPhpDeeplink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        if (value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            {
                return false;
            }

            var host = absolute.Host;
            return host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("www.ecomae.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".ecomae.com", StringComparison.OrdinalIgnoreCase);
        }

        if (!value.StartsWith('/'))
        {
            return false;
        }

        // Reject ASP.NET preview/digest routes (lowercase app surfaces).
        if (value.StartsWith("/cp/", StringComparison.Ordinal)
            || value.StartsWith("/erp/", StringComparison.Ordinal)
            || value.StartsWith("/bos/", StringComparison.Ordinal)
            || value.StartsWith("/storefront/", StringComparison.Ordinal)
            || value.StartsWith("/migration/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // PHP product chrome / shell entry points + legacy content/shop PHP paths.
        return value.StartsWith("/CP", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/ERP", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/BOS", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/content/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal);
    }
}
