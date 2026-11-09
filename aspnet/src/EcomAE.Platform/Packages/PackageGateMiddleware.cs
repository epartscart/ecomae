using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>
/// API-level package gating (Slice 1c): any request path under a package-declared
/// prefix returns 403 <c>package_disabled</c> unless the package is enabled for the
/// resolving tenant. Fail closed: resolver errors are treated as disabled.
/// Must run after <see cref="Middleware.TenantResolutionMiddleware"/> so the tenant is known.
/// </summary>
public sealed class PackageGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IReadOnlyList<IPackageRouteContributor> _contributors;
    private readonly ILogger<PackageGateMiddleware> _log;

    public PackageGateMiddleware(
        RequestDelegate next,
        IEnumerable<IPackageRouteContributor> contributors,
        ILogger<PackageGateMiddleware> log)
    {
        _next = next;
        _contributors = contributors.ToList();
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = tenant?.SiteKey ?? string.Empty;

        foreach (var contributor in _contributors)
        {
            var matched = false;
            foreach (var prefix in contributor.RoutePrefixes)
            {
                if (context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                continue;
            }

            var enabled = false;
            try
            {
                var gate = context.RequestServices.GetService<IPackageGate>();
                enabled = gate is not null
                    && !string.IsNullOrWhiteSpace(siteKey)
                    && await gate.IsEnabledAsync(siteKey, contributor.PackageId, context.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "package route gate {PackageId} failed closed (site {SiteKey})", contributor.PackageId, siteKey);
            }

            if (!enabled)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"ok\":false,\"error\":\"package_disabled\",\"package\":\"" + contributor.PackageId + "\"}",
                    context.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
