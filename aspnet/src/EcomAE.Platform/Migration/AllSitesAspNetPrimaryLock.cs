using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Cross-surface lock: every product site class is ASP.NET-primary.
/// PHP stays under /php-reference/* only. cutoverAllowed / readyForPhpRemoval stay false.
/// </summary>
public static class AllSitesAspNetPrimaryLock
{
    public sealed record SiteClass(
        string Id,
        string Label,
        IReadOnlyList<string> Hosts,
        IReadOnlyList<string> ProductSurfaces,
        string BosPolicy,
        string PhpAccess);

    public static readonly IReadOnlyList<SiteClass> Classes =
    [
        new(
            "super-cp",
            "Super CP / platform (www + cp.ecomae.com)",
            PlatformHostPolicy.SuperCpHosts.ToArray(),
            ["/", "/cp", "/erp", "/bos", "/ip", "/lifeos", "/marketing"],
            "super-cp-only",
            "/php-reference/* (including bos on Super-CP)"),
        new(
            "product-tenants",
            "Named live product tenants",
            LiveTenantPresentationLock.AllHosts.ToArray(),
            ["/", "/cp", "/erp"],
            "super-cp-only-404-on-tenant",
            "/php-reference/* only (bos excluded)"),
        new(
            "industry-showcase",
            "Industry showcase frontends (*.ecomae.com)",
            EcomaeIndustryShowcaseHosts.All
                .Select(h => $"{h.Slug}.ecomae.com")
                .ToArray(),
            ["/", "/cp", "/erp"],
            "super-cp-only-404-on-industry",
            "/php-reference/* only (bos excluded)"),
        new(
            "lifeos",
            "LifeOS customer product",
            PlatformHostPolicy.LifeOsHosts.ToArray(),
            ["/", "/lifeos", "/join", "/clients"],
            "not-applicable",
            "/php-reference/* when present"),
    ];

    public const string Mandate =
        "ALL site classes — Super CP (www/cp.ecomae.com), named product tenants, "
        + "28 industry showcase hosts, and LifeOS — use ASP.NET Core for product chrome. "
        + "Product /bos and /ip remain Super-CP only (tenant + industry hosts 404). "
        + "PHP is not mixed into product; open only via /php-reference/*. "
        + "cutoverAllowed=false and readyForPhpRemoval=false while PHP is kept as reference.";

    public static readonly IReadOnlyList<string> SetLiveCriteria =
    [
        "CloudPanel root: ECOMAE_BRANCH=<branch|main> bash scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh",
        "Classic-entry on www + every named tenant: ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES "
            + "ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES "
            + "bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts",
        "Probe: bash scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh --all-hosts",
        "Expect: no warmup splash on product /; tenant /bos → 404; Super-CP /bos → ASP.NET; "
            + "industry / /cp /erp → ASP.NET; LifeOS home → ASP.NET (not Cloudflare 502).",
        "PHP compare only via /php-reference/* — never product /CP|/ERP|/BOS trees.",
        "Keep RequirePhpFallback=true and writes PHP-authoritative until dual-sample-green per exact write route.",
        "Never invent RELEASE_OWNER_APPROVAL.md or set readyForPhpRemoval=true without evidence.",
    ];

    public static int IndustryHostCount => EcomaeIndustryShowcaseHosts.Count;

    public static int ProductTenantCount => LiveTenantPresentationLock.Tenants.Count;

    public static IReadOnlyDictionary<string, object> BuildSummary()
    {
        var probePath = "docs/migration/evidence/all-sites/all-sites-aspnet-primary-probe.json";
        return new Dictionary<string, object>
        {
            ["policy"] = "aspnet-primary-all-sites-php-reference-only",
            ["targetEndState"] = "100%-aspnet-core-live-php-reference-kept",
            ["mandate"] = Mandate,
            ["cutoverAllowed"] = false,
            ["readyForPhpRemoval"] = false,
            ["phpPrimaryUntilParity"] = false,
            ["stackToday"] = "aspnet",
            ["siteClassCount"] = Classes.Count,
            ["productTenantCount"] = ProductTenantCount,
            ["industryHostCount"] = IndustryHostCount,
            ["superCpHosts"] = PlatformHostPolicy.SuperCpHosts,
            ["lifeOsHosts"] = PlatformHostPolicy.LifeOsHosts,
            ["setLiveCriteria"] = SetLiveCriteria,
            ["forceLiveScript"] = "scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh",
            ["classicEntryScript"] = "scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts",
            ["probeEvidence"] = probePath,
            ["relatedBoards"] = new[]
            {
                "/migration/live-tenant-presentation-lock",
                "/migration/live-surface-links",
                "/migration/marketing-presentation-lock",
                "/migration/php-reference-mode",
                "/migration/aspnet-zero-php-path",
            },
            ["classes"] = Classes.Select(c => new Dictionary<string, object>
            {
                ["id"] = c.Id,
                ["label"] = c.Label,
                ["hosts"] = c.Hosts,
                ["hostCount"] = c.Hosts.Count,
                ["productSurfaces"] = c.ProductSurfaces,
                ["bos"] = c.BosPolicy,
                ["stackToday"] = "aspnet",
                ["targetStack"] = "aspnet",
                ["phpAccess"] = c.PhpAccess,
                ["gate"] = "aspnet-primary-installed-php-reference-separate",
            }).ToArray(),
            ["notes"] = new[]
            {
                "Code + nginx classic-entry define the set; live prove still needs CloudPanel FORCE_LIVE (root SSH).",
                "Warmup splash / Cloudflare 502 means :5100 down or classic-entry not installed — not a policy flip back to PHP.",
                "Industry showcase hosts share ASP.NET /cp /erp shells when classic-entry is on their vhost.",
                "Marketing www home is ASP.NET /marketing/app via classic-entry; PHP compare under /php-reference/home.",
                "Writes remain PHP-authoritative; dry-runs writes=0; cutoverAllowed=false.",
            },
        };
    }
}
