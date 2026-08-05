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
        if (path.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/epc-static", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        if (PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(combined, out var aspNet)
            && !string.Equals(aspNet, combined, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location = aspNet;
            context.Response.Headers["X-EcomAE-Php-Product-Redirect"] = "aspnet-primary";
            return Task.CompletedTask;
        }

        return _next(context);
    }
}
