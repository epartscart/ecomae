using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Deep uppercase PHP product paths (/CP/…, /ERP/…, /BOS/…, /shop/…) never stay as product URLs.
/// Redirect into ASP.NET browse routes. PHP remains reachable only via /php-reference/*.
/// </summary>
public sealed class PhpProductPathRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public PhpProductPathRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
        var combined = path + query;

        // /php-reference/* is the only intentional PHP compare entry — do not rewrite.
        // Asset bridges (/epc-static.php, *_css.php) stay on ASP.NET MapGet handlers.
        if (path.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/epc-static", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/content/general_pages/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/epc_oauth_", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        // Product front-controllers that must never stay on PHP-FPM when they hit Kestrel.
        if (path.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(context, "/" + query); // query already includes leading '?'
        }

        if (path.Equals("/epc-blockchain-verify.php", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(context, "/blockchain/verify" + query);
        }

        if (PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(combined, out var aspNet)
            && !string.Equals(aspNet, combined, StringComparison.Ordinal))
        {
            return Redirect(context, aspNet);
        }

        return _next(context);
    }

    private static Task Redirect(HttpContext context, string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            location = "/";
        }
        else if (location[0] != '/')
        {
            location = "/" + location.TrimStart('/');
        }

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = location;
        context.Response.Headers["X-EcomAE-Php-Product-Redirect"] = "aspnet-primary";
        return Task.CompletedTask;
    }
}
