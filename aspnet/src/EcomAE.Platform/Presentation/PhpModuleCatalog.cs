namespace EcomAE.Platform.Presentation;

/// <summary>
/// Helpers over the generated PHP module directory.
/// Interactive module bodies remain on PHP; ASP.NET chrome lists every surface so none are omitted.
/// </summary>
public static partial class PhpModuleCatalog
{
    public static IReadOnlyDictionary<string, object> BuildSummary() => new Dictionary<string, object>
    {
        ["policy"] = "hybrid-deeplink-to-php-until-aspnet-module-complete",
        ["erpCategories"] = ErpCategoryCount,
        ["erpAreas"] = ErpAreaCount,
        ["erpTabs"] = ErpTabCount,
        ["bosModules"] = BosModuleCount,
        ["cpBrochureFeatures"] = CpBrochureFeatureCount,
        ["storefrontSurfaces"] = StorefrontSurfaceCount,
        ["totalTracked"] = ErpCategoryCount + ErpAreaCount + ErpTabCount + BosModuleCount + CpBrochureFeatureCount + StorefrontSurfaceCount,
        ["aspNetInteractiveComplete"] = 0,
        ["notes"] = new[]
        {
            "Live product chrome remains PHP (/CP/ /ERP/ /BOS/ storefront hosts).",
            "ASP.NET /cp|/erp|/bos|/storefront/app shells expose this full directory via hybrid deeplinks.",
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
}
