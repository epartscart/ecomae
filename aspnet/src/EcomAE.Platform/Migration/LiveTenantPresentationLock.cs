namespace EcomAE.Platform.Migration;

/// <summary>
/// Named live production tenants — product chrome is ASP.NET Core for all.
/// PHP stays available only as reference under /php-reference/* (not mixed into product).
/// cutoverAllowed / readyForPhpRemoval stay false (reference keep ≠ PHP source deletion).
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

    /// <summary>Nginx <c>$host</c> allowlist for classic-entry tenant pack (all named product tenants).</summary>
    public const string NginxProductHostRegex =
        @"^(www\.)?(epartscart|electronicae|stylenlook|thejewellerytrend|taxofinca)\.com$";

    public const string Mandate =
        "ALL named live product tenants use ASP.NET Core for / /cp /erp and deep product trees. "
        + "Product BOS (/bos) is Super-CP / platform only (www.ecomae.com, ecomae.com, cp.ecomae.com) — "
        + "tenant hosts must 404 /bos (confidential fleet ops must not leak). "
        + "PHP is NOT mixed into product — open only via /php-reference/* for compare/archive "
        + "(except /php-reference/bos which is also Super-CP-only). "
        + "cutoverAllowed=false and readyForPhpRemoval=false while PHP project is kept as reference.";

    public static readonly IReadOnlyList<string> UnlockCriteria =
    [
        "Install classic-entry ASP.NET primary on www + every named product tenant server block (--all-hosts).",
        "Tenant classic-entry must return 404 for /bos|/BOS|/bos/login|/php-reference/bos (never proxy BOS).",
        "Redeploy ASP.NET binary so BosHostGateMiddleware 404s /bos on tenant Host headers.",
        "PHP compare only via /php-reference/* → index.php (never product /CP|/ERP|/shop trees).",
        "Keep RequirePhpFallback=true until dual-sample-green per exact write route.",
        "PHP source removal still needs separate RELEASE_OWNER_APPROVAL.md (never invent).",
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

    public static bool IsProductTenantHost(string? host) => IsLockedHost(host);

    public static IReadOnlyDictionary<string, object> BuildSummary() => new Dictionary<string, object>
    {
        ["policy"] = "aspnet-primary-all-product-tenants-php-reference-only",
        ["targetEndState"] = "100%-aspnet-core-live-php-reference-kept",
        ["mandate"] = Mandate,
        ["cutoverAllowed"] = false,
        ["readyForPhpRemoval"] = false,
        ["phpPrimaryUntilParity"] = false,
        ["stackToday"] = "aspnet",
        ["tenantCount"] = Tenants.Count,
        ["hosts"] = AllHosts,
        ["nginxProductHostRegex"] = NginxProductHostRegex,
        ["unlockCriteria"] = UnlockCriteria,
        ["parityShadowConfirmEnv"] = "ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW",
        ["tenants"] = Tenants.Select(t => new Dictionary<string, object>
        {
            ["id"] = t.Id,
            ["label"] = t.Label,
            ["hosts"] = t.Hosts,
            ["surfaces"] = new[] { "storefront", "cp", "erp" },
            ["bos"] = "super-cp-only-404-on-tenant",
            ["stackToday"] = "aspnet",
            ["targetStack"] = "aspnet",
            ["phpAccess"] = "/php-reference/* only (bos excluded)",
            ["gate"] = "aspnet-primary-installed-php-reference-separate-bos-super-cp-only",
        }).ToArray(),
        ["verify"] = "bash scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh --all-hosts",
        ["docs"] = new[]
        {
            "docs/migration/PHP_AS_REFERENCE_MODE.md",
            "docs/migration/TENANT_MIGRATION_SAFETY.md",
            "docs/migration/ASPNET_ZERO_PHP_PATH.md",
        },
        ["notes"] = new[]
        {
            "No half-and-half: every named product tenant is ASP.NET-primary for / /cp /erp.",
            "BOS is Super-CP only — epartscart.com/bos and other tenants must 404.",
            "Install classic-entry on all tenant server blocks with ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES --all-hosts.",
            "Industry *.ecomae.com showcase hosts use the same ASP.NET /cp /erp shells when classic-entry is installed on their vhost.",
            "Never invent RELEASE_OWNER_APPROVAL.md or set readyForPhpRemoval=true without evidence.",
        },
    };
}
