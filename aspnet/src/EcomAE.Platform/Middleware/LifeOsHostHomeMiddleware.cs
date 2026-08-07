using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// On <c>lifeos.ecomae.com</c>, send visitors to the LifeOS product home.
/// Uses an HTTP redirect (not an in-pipeline Path rewrite): with the implicit
/// <c>UseRouting</c> at the start of the ASP.NET pipeline, changing
/// <see cref="HttpRequest.Path"/> after endpoint matching still executes the
/// already-selected <c>/marketing/app</c> page (live symptom: header
/// <c>X-EcomAE-LifeOs-Host: marketing-divert</c> with ECOM AE marketing body).
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
        if (!ShouldRewriteToLifeOsHome(path))
        {
            return _next(context);
        }

        // Already on the product home — do not redirect-loop.
        if (path.Equals(EcomAeRoutes.LifeOs, StringComparison.OrdinalIgnoreCase)
            || path.Equals(EcomAeRoutes.LifeOs + "/", StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        var reason = path is "/" or "" ? "home-redirect" : "marketing-divert";
        context.Response.Headers["X-EcomAE-LifeOs-Host"] = reason;
        context.Response.Headers["X-EcomAE-LifeOs-From"] = path;
        var target = EcomAeRoutes.LifeOs + context.Request.QueryString.Value;
        context.Response.Redirect(target, permanent: false);
        return Task.CompletedTask;
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
