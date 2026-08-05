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
            "api" => TenantSurface.Api,
            _ => TenantSurface.Storefront
        };

        var registryRecord = await _tenantRegistry.FindByHostAsync(host, cancellationToken);
        var mode = registryRecord?.Mode ?? (PlatformHostPolicy.IsSuperCpHost(host)
            ? TenantMode.Platform
            : surface == TenantSurface.Erp ? TenantMode.ErpOnlyTenant : TenantMode.LiveTenant);

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
