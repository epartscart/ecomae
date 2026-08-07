using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// On <c>lifeos.ecomae.com</c>, serve the LifeOS product home at <see cref="EcomAeRoutes.LifeOs"/>.
/// Rewrites bare <c>/</c> and mis-routed www marketing paths (<c>/marketing/app</c>) that
/// classic-entry nginx may inject when the lifeos host shares the www server block.
/// </summary>
public sealed class LifeOsHostHomeMiddleware
{
    private readonly RequestDelegate _next;

    public LifeOsHostHomeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (!PlatformHostPolicy.IsLifeOsHost(host))
        {
            return _next(context);
        }

        var path = context.Request.Path.Value ?? "/";
        if (ShouldRewriteToLifeOsHome(path))
        {
            context.Request.Path = EcomAeRoutes.LifeOs;
            context.Response.Headers["X-EcomAE-LifeOs-Host"] = path is "/" or ""
                ? "home-rewrite"
                : "marketing-divert";
        }

        return _next(context);
    }

    public static bool ShouldRewriteToLifeOsHome(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return true;
        }

        // Classic-entry www home proxies `/` → `/marketing/app`. If that snippet is
        // applied on the lifeos host, divert back to the LifeOS product.
        if (path.Equals("/marketing/app", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/marketing/app/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/marketing", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/marketing/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
