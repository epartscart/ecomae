namespace EcomAE.Platform.Migration;

/// <summary>
/// Named live production tenants under same-to-same parity gate.
/// Target end-state is 100% ASP.NET Core / 0 PHP — not permanent PHP.
/// Until dual-sample same-to-same + exact-route promotion evidence exists,
/// product chrome stays on PHP so tenants feel zero change. cutoverAllowed stays false
/// until that gate clears (never invent RELEASE_OWNER_APPROVAL.md).
/// </summary>
public static class LiveTenantPresentationLock
{
    public sealed record LockedTenant(string Id, string Label, IReadOnlyList<string> Hosts);

    public static readonly IReadOnlyList<LockedTenant> Tenants =
    [
        new("epartscart", "ePartsCart", ["epartscart.com", "www.epartscart.com"]),
        new("electronicae", "Electronicae", ["www.electronicae.com", "electronicae.com"]),
        new("stylenlook", "StyleNLook", ["www.stylenlook.com", "stylenlook.com"]),
        new("thejewellerytrend", "The Jewellery Trend", ["www.thejewellerytrend.com", "thejewellerytrend.com"]),
        new("taxofinca", "Taxofinca", ["www.taxofinca.com", "taxofinca.com"]),
    ];

    public static IReadOnlyList<string> AllHosts { get; } =
        Tenants.SelectMany(t => t.Hosts).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public const string Mandate =
        "TARGET: 100% ASP.NET Core / 0 PHP. Named live tenants stay PHP-primary only until "
        + "ASP.NET same-to-same dual-sample parity + exact-route staged cutover. "
        + "Presentation (theme/colour/structure/fonts/hero/fields) must match PHP during migration "
        + "so tenants feel zero change. cutoverAllowed=false until the parity gate clears.";

    public static readonly IReadOnlyList<string> UnlockCriteria =
    [
        "ASP.NET storefront/CP/ERP chrome same-to-same with PHP (dual-sample evidence per surface).",
        "Exact-route shadows proven on www, then staged per-tenant with ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES.",
        "Interactive module function parity (menus/forms/writes) dual-sample green.",
        "Human RELEASE_OWNER_APPROVAL.md for that host/surface (never invent this file).",
        "Then exact-route cutover → observation window → PHP removal for that surface.",
    ];

    public static bool IsLockedHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        return AllHosts.Any(x => string.Equals(x, h, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyDictionary<string, object> BuildSummary() => new Dictionary<string, object>
    {
        ["policy"] = "parity-gate-until-aspnet-same-to-same-then-cutover",
        ["targetEndState"] = "100%-aspnet-core-0-php",
        ["mandate"] = Mandate,
        ["cutoverAllowed"] = false,
        ["readyForPhpRemoval"] = false,
        ["phpPrimaryUntilParity"] = true,
        ["tenantCount"] = Tenants.Count,
        ["hosts"] = AllHosts,
        ["unlockCriteria"] = UnlockCriteria,
        ["parityShadowConfirmEnv"] = "ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW",
        ["tenants"] = Tenants.Select(t => new Dictionary<string, object>
        {
            ["id"] = t.Id,
            ["label"] = t.Label,
            ["hosts"] = t.Hosts,
            ["surfaces"] = new[] { "storefront", "cp", "erp" },
            ["stackToday"] = "php",
            ["targetStack"] = "aspnet",
            ["gate"] = "same-to-same-dual-sample-then-exact-route",
        }).ToArray(),
        ["verify"] = "bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh",
        ["docs"] = new[]
        {
            "docs/migration/TENANT_MIGRATION_SAFETY.md",
            "docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md",
            "docs/migration/ASPNET_ZERO_PHP_PATH.md",
        },
        ["notes"] = new[]
        {
            "Not a permanent PHP ban — a parity gate protecting same-to-same UX while ASP.NET is completed.",
            "Default refuse ASP.NET shadows on named tenant vhosts; unlock exact-route parity shadows with ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES.",
            "www.ecomae.com remains the primary scaffolding host for digests/hybrid apps until tenant cutover.",
            "Never invent RELEASE_OWNER_APPROVAL.md or set cutoverAllowed=true without evidence.",
        },
    };
}
