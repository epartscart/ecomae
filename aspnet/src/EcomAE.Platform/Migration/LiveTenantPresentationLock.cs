namespace EcomAE.Platform.Migration;

/// <summary>
/// Named live production tenants whose storefront / CP / ERP presentation must remain
/// identical to PHP (theme, colour, structure, fonts, hero/splash, fields).
/// ASP.NET hybrid previews and digests are www.ecomae.com only — never these hosts.
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
        "Named live tenants must keep storefront + CP + ERP presentation same-to-same with PHP "
        + "(theme, colouring, structure, fonts, hero/splash, fields). "
        + "No ASP.NET hybrid chrome on those hosts. cutoverAllowed=false.";

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
        ["policy"] = "live-tenant-presentation-identical-to-php",
        ["mandate"] = Mandate,
        ["cutoverAllowed"] = false,
        ["readyForPhpRemoval"] = false,
        ["tenantCount"] = Tenants.Count,
        ["hosts"] = AllHosts,
        ["tenants"] = Tenants.Select(t => new Dictionary<string, object>
        {
            ["id"] = t.Id,
            ["label"] = t.Label,
            ["hosts"] = t.Hosts,
            ["surfaces"] = new[] { "storefront", "cp", "erp" },
            ["stack"] = "php",
        }).ToArray(),
        ["verify"] = "bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh",
        ["docs"] = "docs/migration/TENANT_MIGRATION_SAFETY.md",
        ["notes"] = new[]
        {
            "ASP.NET /cp|/erp|/bos|/storefront/app and digests are scaffolding on www.ecomae.com only.",
            "Installer hard-refuses shadows on named live tenant nginx confs (ecomae_nginx_site_safety.py).",
            "Never invent RELEASE_OWNER_APPROVAL.md.",
        },
    };
}
