using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Thin ASP.NET <c>/marketing/{slug}</c> stubs redirect to PHP canonical full pages.
/// Home surface <c>/marketing/app</c> stays on ASP.NET (proxied as www <c>/</c>).
/// </summary>
public sealed class MarketingStubToPhpRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public MarketingStubToPhpRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!path.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/marketing", StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
        var combined = path + query;
        if (!EcomaeMarketingPages.TryMapMarketingStubToPhp(combined, out var phpCanonical)
            || string.Equals(phpCanonical, combined, StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = phpCanonical;
        context.Response.Headers["X-EcomAE-Marketing-Stub-Redirect"] = "php-canonical";
        return Task.CompletedTask;
    }
}
