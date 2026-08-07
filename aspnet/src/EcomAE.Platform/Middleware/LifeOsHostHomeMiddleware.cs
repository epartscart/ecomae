using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// On <c>lifeos.ecomae.com</c>, bare <c>/</c> serves the LifeOS product home
/// by rewriting to <see cref="EcomAeRoutes.LifeOs"/>.
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
        if (path is "/" or "")
        {
            context.Request.Path = EcomAeRoutes.LifeOs;
            context.Response.Headers["X-EcomAE-LifeOs-Host"] = "home-rewrite";
        }

        return _next(context);
    }
}
