using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// lifeos.ecomae.com is retired. Send every visitor to the ECOM AE site.
/// Health and release probes still pass through so the host can be checked.
/// </summary>
public sealed class LifeOsHostHomeMiddleware
{
    public const string RetiredHome = "https://www.ecomae.com/";

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
        if (IsOpsPath(path))
        {
            return _next(context);
        }

        context.Response.Headers["X-EcomAE-LifeOs-Host"] = "retired";
        context.Response.Headers["X-EcomAE-LifeOs-From"] = path;
        context.Response.Redirect(RetiredHome, permanent: false);
        return Task.CompletedTask;
    }

    public static bool IsOpsPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return false;
        }

        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/migration", StringComparison.OrdinalIgnoreCase);
    }
}
