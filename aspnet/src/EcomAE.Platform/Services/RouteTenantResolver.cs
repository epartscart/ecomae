using EcomAE.Platform.Configuration;
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

        var mode = registryRecord?.Mode
                   ?? (surface == TenantSurface.Erp ? TenantMode.ErpOnlyTenant : TenantMode.LiveTenant);

        return new TenantContext(
            host,
            path,
            surface,
            mode,
            registryRecord?.SiteKey,
            registryRecord?.DatabaseName,
            registryRecord?.DbUser,
            registryRecord?.DbPassword,
            registryRecord?.DedicatedDb ?? false);
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
