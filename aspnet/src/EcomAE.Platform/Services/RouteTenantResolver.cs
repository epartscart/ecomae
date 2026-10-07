using EcomAE.Platform.Configuration;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Services;

public sealed class RouteTenantResolver : ITenantResolver
{
    private readonly EcomAeOptions _options;
    private readonly ITenantRegistry _tenantRegistry;

    public RouteTenantResolver(IOptions<EcomAeOptions> options, ITenantRegistry tenantRegistry)
    {
        _options = options.Value;
        _tenantRegistry = tenantRegistry;
    }

    public async ValueTask<TenantContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var host = httpContext.Request.Host.Host.ToLowerInvariant();
        var path = NormalizePath(httpContext.Request.Path.Value);
        var first = FirstSegment(path);
        var surface = first switch
        {
            "cp" => TenantSurface.ControlPanel,
            "erp" => TenantSurface.Erp,
            "bos" => TenantSurface.Bos,
            "ip" => TenantSurface.Ip,
            "lifeos" => TenantSurface.LifeOs,
            "api" => TenantSurface.Api,
            _ => TenantSurface.Storefront
        };

        // lifeos.ecomae.com bare home is the LifeOS product surface.
        if (surface == TenantSurface.Storefront && PlatformHostPolicy.IsLifeOsHost(host))
        {
            surface = TenantSurface.LifeOs;
        }

        var registryRecord = await _tenantRegistry.FindByHostAsync(host, cancellationToken);

        // SUPER-CP ISOLATION: platform hosts (www.ecomae.com / ecomae.com / cp.ecomae.com)
        // are ALWAYS Platform mode with the platform database. PHP registers erp-only
        // shared tenants under the www hostname in epc_portal_tenants — a live registry
        // row must never bind the super host's CP/ERP to a TENANT database
        // (that leaked epartscart data onto ecomae.com/cp and /erp).
        if (PlatformHostPolicy.IsSuperCpHost(host))
        {
            var platformRecord = registryRecord?.Mode == TenantMode.Platform ? registryRecord : null;
            return new TenantContext(
                host,
                path,
                surface,
                TenantMode.Platform,
                platformRecord?.SiteKey ?? "platform",
                platformRecord?.DatabaseName ?? PlatformSeedDatabase(),
                platformRecord?.DbUser,
                platformRecord?.DbPassword,
                platformRecord?.DedicatedDb ?? false);
        }

        // Industry showcase hosts ({slug}.ecomae.com) — platform-managed marketing demos,
        // not client shop DBs. CP/ERP shells still use platform registry credentials.
        if (EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out var industrySlug))
        {
            var platformRecord = registryRecord?.Mode == TenantMode.Platform ? registryRecord : null;
            return new TenantContext(
                host,
                path,
                surface,
                TenantMode.IndustrySubdomain,
                "industry-" + industrySlug,
                platformRecord?.DatabaseName ?? PlatformSeedDatabase(),
                platformRecord?.DbUser,
                platformRecord?.DbPassword,
                DedicatedDb: false);
        }

        var mode = registryRecord?.Mode
                   ?? (surface == TenantSurface.Erp ? TenantMode.ErpOnlyTenant : TenantMode.LiveTenant);

        var siteKey = registryRecord?.SiteKey;
        var databaseName = registryRecord?.DatabaseName;
        var dbUser = registryRecord?.DbUser;
        var dbPassword = registryRecord?.DbPassword;
        var dedicated = registryRecord?.DedicatedDb ?? false;

        // PHP portal parity (epc_portal_resolve_tenant_db_credentials): ePartsCart
        // storefront/CP uses shared Model C `docpart` when portal db_name is empty.
        // Without this, CP login returns tenant_db_unbound even though the shop DB exists.
        if (string.IsNullOrWhiteSpace(databaseName) && IsEpartsCartHost(host, siteKey))
        {
            databaseName = "docpart";
            siteKey ??= "epartscart";
            // Do not invent db_user/password — TenantRegistry CS credentials open `docpart`.
            mode = TenantMode.LiveTenant;
        }

        // PHP epc_portal_sites() names these hosts, and epc_portal_resolve_tenant_db()
        // binds them to docpart when epc_portal_tenants has no dedicated row. That bind
        // is the degraded shared-docpart fallback. A registry database still wins.
        if (string.IsNullOrWhiteSpace(databaseName) && IsDegradedSharedShopHost(host))
        {
            databaseName = "docpart";
            siteKey ??= DegradedShopSiteKey(host);
            mode = TenantMode.LiveTenant;
            dedicated = false;
        }

        // industries.ecomae.com is a platform hostname in PHP. /cp uses the platform
        // operator database, not a shop database and not a new portal row.
        if (string.IsNullOrWhiteSpace(databaseName)
            && EcomaeIndustryShowcaseSnapshots.IsIndustriesDirectoryHost(host)
            && surface is TenantSurface.ControlPanel or TenantSurface.Erp or TenantSurface.Bos)
        {
            return new TenantContext(
                host,
                path,
                surface,
                TenantMode.Platform,
                "platform",
                PlatformSeedDatabase(),
                null,
                null,
                DedicatedDb: false);
        }

        return new TenantContext(
            host,
            path,
            surface,
            mode,
            siteKey,
            databaseName,
            dbUser,
            dbPassword,
            dedicated);
    }

    /// <summary>True for epartscart.com / www.epartscart.com or site_key epartscart.</summary>
    public static bool IsEpartsCartHost(string host, string? siteKey)
    {
        if (!string.IsNullOrWhiteSpace(siteKey)
            && siteKey.StartsWith("epartscart", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var h = PlatformHostPolicy.NormalizeHost(host);
        if (h.StartsWith("www.", StringComparison.Ordinal))
        {
            h = h[4..];
        }

        return string.Equals(h, "epartscart.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Named shops in <c>epc_portal_sites()</c> other than eParts Cart. PHP still opens
    /// shared <c>docpart</c> when no dedicated <c>epc_portal_tenants</c> database is stored.
    /// </summary>
    public static bool IsDegradedSharedShopHost(string host)
    {
        var h = BareHost(host);
        return h is "electronicae.com" or "stylenlook.com" or "thejewellerytrend.com" or "taxofinca.com";
    }

    public static string DegradedShopSiteKey(string host)
        => BareHost(host) switch
        {
            "electronicae.com" => "electronicae",
            "stylenlook.com" => "stylenlook",
            "thejewellerytrend.com" => "thejewellerytrend",
            "taxofinca.com" => "taxofinca",
            _ => string.Empty,
        };

    private static string BareHost(string host)
    {
        var h = PlatformHostPolicy.NormalizeHost(host);
        return h.StartsWith("www.", StringComparison.Ordinal) ? h[4..] : h;
    }

    private string? PlatformSeedDatabase()
    {
        var seed = _options.SeedTenants.FirstOrDefault(t =>
            t.Mode == TenantMode.Platform
            || string.Equals(t.SiteKey, "platform", StringComparison.OrdinalIgnoreCase));
        var db = seed?.DatabaseName;
        return string.IsNullOrWhiteSpace(db) ? "ecomae" : db.Trim();
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = "/" + path.Replace('\\', '/').Trim('/');
        return normalized == "//" ? "/" : normalized;
    }

    private static string FirstSegment(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
    }

}
