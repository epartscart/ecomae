using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Services;

public sealed class RouteTenantResolver : ITenantResolver
{
    private readonly EcomAeOptions _options;
    private readonly ITenantRegistry _tenantRegistry;
    private readonly IClientErpTenantLookup _clientErpTenants;

    public RouteTenantResolver(
        IOptions<EcomAeOptions> options,
        ITenantRegistry tenantRegistry,
        IClientErpTenantLookup? clientErpTenants = null)
    {
        _options = options.Value;
        _tenantRegistry = tenantRegistry;
        _clientErpTenants = clientErpTenants ?? NullClientErpTenantLookup.Instance;
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

        // D365-style dedicated tenant hosts: {site_key}.operations.{country3}.ecomae.com.
        // The hostname binds the tenant exactly like /cp/client-erp/{key} does on the
        // Super host, so the tenant lands on its own industry ERP with no CP path.
        if (ErpOperationsHost.TryParse(host, out var operationsKey, out _))
        {
            var bound = await TryResolveErpOnlyAsync(host, path, operationsKey, cancellationToken);
            if (bound is not null)
            {
                return bound;
            }
            // Unknown slug: fall through to the normal host/registry resolution.
        }

        // UAT tier (D365 sandbox): {site_key}-sandbox-{country3}.ecomae.com binds the
        // same registry row but opens {database}_sandbox instead of the live DB.
        if (ErpOperationsHost.TryParseSandbox(host, out var sandboxKey, out _))
        {
            var bound = await TryResolveErpOnlyAsync(host, path, sandboxKey, cancellationToken, sandbox: true);
            if (bound is not null)
            {
                return bound;
            }
        }

        // SUPER-CP ISOLATION: platform hosts (www.ecomae.com / ecomae.com / cp.ecomae.com)
        // are ALWAYS Platform mode with the platform database. PHP registers erp-only
        // shared tenants under the www hostname in epc_portal_tenants — a live registry
        // row must never bind the super host's CP/ERP to a TENANT database
        // (that leaked epartscart data onto ecomae.com/cp and /erp).
        // Exception: /cp/client-erp/{site_key} is the PHP-parity company ERP URL on the
        // Super host. Bind THAT dedicated DB only for this path; /cp and /erp stay platform.
        if (PlatformHostPolicy.IsSuperCpHost(host))
        {
            if (ClientErpPath.TryParse(path, out var clientErp) && clientErp.SiteKey.Length > 0)
            {
                var bound = await TryResolveErpOnlyAsync(host, path, clientErp.SiteKey, cancellationToken);
                if (bound is not null)
                {
                    return bound;
                }

                // Unknown / unusable key: stay on the platform DB so Super CP isolation
                // holds and the login page can render "not found" without a tenant-DB open.
            }

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
                DedicatedDb: false,
                IndustryCode: industrySlug);
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
            dedicated,
            ResolveIndustryCode(siteKey, null, host));
    }

    /// <summary>
    /// Resolve an ERP-only tenant row (<c>epc_portal_tenants.erp_only_shared</c>) into a
    /// bound tenant context. Shared by Super-CP <c>/cp/client-erp/{key}</c> paths and
    /// dedicated <c>{key}.operations.{country}.ecomae.com</c> hosts.
    /// </summary>
    private async Task<TenantContext?> TryResolveErpOnlyAsync(
        string host,
        string path,
        string siteKey,
        CancellationToken cancellationToken,
        bool sandbox = false)
    {
        ClientErpTenantRecord? erpOnly = null;
        try
        {
            erpOnly = await _clientErpTenants
                .FindActiveErpOnlyAsync(siteKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            erpOnly = null;
        }

        if (erpOnly is null || !erpOnly.IsActive || string.IsNullOrWhiteSpace(erpOnly.DatabaseName))
        {
            return null;
        }

        var dedicatedUser = erpOnly.DbUser;
        var dedicatedPassword = erpOnly.DbPassword;
        if (sandbox)
        {
            if (!TenantContext.UsableDedicatedCredentials(erpOnly.DbUatUser, erpOnly.DbUatPassword))
            {
                // Fail closed: a sandbox host must NEVER authenticate with the
                // production account — provision {user}_uat first
                // (scripts/cloudpanel_sandbox_uat_user.sh).
                return null;
            }
            dedicatedUser = erpOnly.DbUatUser;
            dedicatedPassword = erpOnly.DbUatPassword;
        }
        else if (!TenantContext.UsableDedicatedCredentials(dedicatedUser, dedicatedPassword))
        {
            dedicatedUser = null;
            dedicatedPassword = null;
        }

        var databaseName = sandbox
            ? ErpOperationsHost.SandboxDatabaseName(erpOnly.DatabaseName)
            : erpOnly.DatabaseName;
        if (databaseName.Length == 0)
        {
            return null;
        }

        return new TenantContext(
            host,
            path,
            TenantSurface.Erp,
            TenantMode.ErpOnlyTenant,
            erpOnly.SiteKey,
            databaseName,
            dedicatedUser,
            dedicatedPassword,
            erpOnly.DedicatedDb,
            ResolveIndustryCode(erpOnly.SiteKey, erpOnly.IndustryCode, host),
            DisplayName: TradeNameFor(erpOnly),
            IsSandbox: sandbox);
    }

    private static string? TradeNameFor(ClientErpTenantRecord row)
    {
        if (!string.IsNullOrWhiteSpace(row.TradeName))
        {
            return row.TradeName.Trim();
        }

        return ErpOnlyLaunchClients.All.FirstOrDefault(c =>
            string.Equals(c.SiteKey, row.SiteKey, StringComparison.OrdinalIgnoreCase))?.TradeName;
    }

    /// <summary>
    /// Bound-tenant industry wins, then the ERP-only launch catalog, then the host.
    /// Hybrid jewellery hosts and ERP-only INDJ therefore share the same jewellery pack.
    /// </summary>
    public static string ResolveIndustryCode(string? siteKey, string? portalIndustry, string? host)
    {
        var portal = (portalIndustry ?? string.Empty).Trim();
        if (portal.Length > 0)
        {
            return portal;
        }

        var key = ClientErpPath.NormalizeSiteKey(siteKey);
        var launch = ErpOnlyLaunchClients.All.FirstOrDefault(c =>
            string.Equals(c.SiteKey, key, StringComparison.OrdinalIgnoreCase));
        if (launch is not null && !string.IsNullOrWhiteSpace(launch.IndustryCode))
        {
            return launch.IndustryCode;
        }

        return ErpHostContext.Resolve(host).IndustryCode;
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
