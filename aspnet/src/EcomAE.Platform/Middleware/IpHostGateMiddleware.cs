using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Blocks product Intelligence Platform on tenant hosts. IP is Super-CP / platform only
/// (<c>www.ecomae.com</c>, <c>ecomae.com</c>, <c>cp.ecomae.com</c>) — same isolation as BOS.
/// Named tenants such as epartscart.com must get 404.
/// </summary>
public sealed class IpHostGateMiddleware
{
    private readonly RequestDelegate _next;

    public IpHostGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!PlatformHostPolicy.IsProductIpPath(path))
        {
            return _next(context);
        }

        var host = context.Request.Host.Host;
        if (PlatformHostPolicy.IsSuperCpHost(host))
        {
            return _next(context);
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.Headers["X-EcomAE-Ip-Host-Gate"] = "super-cp-only";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync("Not found.");
    }
}
