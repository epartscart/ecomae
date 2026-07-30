using EcomAE.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Services;

public sealed class RouteTenantResolver : ITenantResolver
{
    private readonly EcomAeOptions _options;

    public RouteTenantResolver(IOptions<EcomAeOptions> options)
    {
        _options = options.Value;
    }

    public ValueTask<TenantContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
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

        var platformHost = _options.PlatformHost.ToLowerInvariant();
        var mode = host == platformHost || host == TrimWww(platformHost)
            ? TenantMode.Platform
            : surface == TenantSurface.Erp ? TenantMode.ErpOnlyTenant : TenantMode.LiveTenant;

        return ValueTask.FromResult(new TenantContext(host, path, surface, mode));
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

    private static string TrimWww(string host)
    {
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
