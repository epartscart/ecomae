namespace EcomAE.Platform.Presentation;

/// <summary>
/// Helpers over the generated PHP module directory.
/// Primary product clicks rewrite to ASP.NET browse routes; PHP stays under /php-reference/*.
/// </summary>
public static partial class PhpModuleCatalog
{
    public static int MarketingSurfaceCount => EcomaeMarketingPages.Count;

    public static readonly IReadOnlyList<ModuleLink> MarketingSurfaces =
        EcomaeMarketingPages.All
            .Select(p => new ModuleLink(p.Id, p.Label, p.Href, null, p.Group))
            .ToList();

    public static int TotalTrackedCount =>
        ErpCategoryCount
        + ErpAreaCount
        + ErpTabCount
        + BosSectionCount
        + BosModuleCount
        + CpBrochureFeatureCount
        + StorefrontSurfaceCount
        + MarketingSurfaceCount;

    public static IEnumerable<ModuleLink> AllTrackedLinks()
        => ErpCategories
            .Concat(ErpAreas)
            .Concat(ErpTabs)
            .Concat(BosSections)
            .Concat(BosModules)
            .Concat(CpBrochureFeatures)
            .Concat(StorefrontSurfaces)
            .Concat(MarketingSurfaces);

    public static IReadOnlyDictionary<string, object> BuildSummary() => new Dictionary<string, object>
    {
        ["policy"] = "aspnet-primary-browse-php-reference-only",
        ["erpCategories"] = ErpCategoryCount,
        ["erpAreas"] = ErpAreaCount,
        ["erpTabs"] = ErpTabCount,
        ["bosSections"] = BosSectionCount,
        ["bosModules"] = BosModuleCount,
        ["cpBrochureFeatures"] = CpBrochureFeatureCount,
        ["storefrontSurfaces"] = StorefrontSurfaceCount,
        ["marketingSurfaces"] = MarketingSurfaceCount,
        ["totalTracked"] = TotalTrackedCount,
        ["directoryCoverage"] = new Dictionary<string, object>
        {
            ["cpCommandCentre"] = "CpBrochureFeatures",
            ["erpDashboard"] = "ErpCategories+ErpAreas+ErpTabs",
            ["bosFleet"] = "BosSections+BosModules",
            ["storefrontPreview"] = "StorefrontSurfaces",
            ["marketingPreview"] = "MarketingSurfaces",
            ["omittedKinds"] = Array.Empty<string>(),
            ["fullCatalogFloor"] = 725,
        },
        ["aspNetInteractiveComplete"] = 0,
        ["cutoverAllowed"] = false,
        ["readyForPhpRemoval"] = false,
        ["deeplinkFloorOk"] = AllTrackedLinks().All(link => IsAllowedTrackedHref(link.Href)),
        ["notes"] = new[]
        {
            "Live shared entries / /cp /erp /bos are ASP.NET (PHP style chrome).",
            "PHP product pages open only via /php-reference/* — not from primary chrome clicks.",
            "ASP.NET /cp|/erp|/bos|/storefront|/marketing/app shells expose this full directory; primary hrefs rewrite to ASP.NET browse routes.",
            "ERP shells list categories + areas + tabs; CP lists all brochure features; BOS sections + modules; storefront all surfaces; marketing all pages.",
            "cutoverAllowed stays false until dual-sample gates pass for deep modules."
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
    /// Allowed tracked catalog hrefs under ASP.NET-primary policy:
    /// ASP.NET browse routes and/or legacy PHP deeplinks (rewritten at click time).
    /// </summary>
    public static bool IsAllowedTrackedHref(string? href)
        => IsAllowedAspNetBrowseHref(href) || IsAllowedPhpDeeplink(href);

    /// <summary>ASP.NET product browse routes used as primary catalog hrefs.</summary>
    public static bool IsAllowedAspNetBrowseHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        return value.Equals("/", StringComparison.Ordinal)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Allowed hybrid iframe / PHP-reference targets: PHP CP/ERP/BOS paths or storefront absolute/relative URLs.
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
            || value.StartsWith("/marketing/", StringComparison.Ordinal)
            || value.StartsWith("/migration/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // PHP product chrome / shell entry points + legacy content/shop PHP paths + marketing.
        return value.StartsWith("/CP", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/ERP", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/BOS", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/content/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal)
            || EcomaeMarketingPages.IsMarketingPhpPath(value);
    }
}
