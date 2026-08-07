using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Thin ASP.NET <c>/marketing/{slug}</c> stubs redirect to PHP-canonical URLs.
/// Those URLs are served as PHP-rendered snapshots by
/// <see cref="EcomaeMarketingSnapshotMiddleware"/> (full marketing site), not the
/// scaffold Overview components. Home surface <c>/marketing/app</c> stays on Blazor
/// (proxied as www <c>/</c>).
/// Always redirect — even when product PHP HTTP is paused — so the public site never
/// shows Unsplash/scaffold stubs.
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
        context.Response.Headers["X-EcomAE-Marketing-Stub-Redirect"] = "snapshot-canonical";
        return Task.CompletedTask;
    }
}
