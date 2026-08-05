using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Thin / broken ASP.NET <c>/storefront/*</c> stubs redirect to live PHP canonical pages
/// (<c>/en/shop/warehouse-search</c>, <c>/en/umapi_catalog</c>, …).
/// Home <c>/storefront/app</c> stays ASP.NET.
/// </summary>
public sealed class StorefrontStubToPhpRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public StorefrontStubToPhpRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!path.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
        var combined = path + query;
        if (!StorefrontPhpCanonical.TryMapStorefrontStubToPhp(combined, out var php)
            || string.Equals(php, combined, StringComparison.OrdinalIgnoreCase))
        {
            return _next(context);
        }

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = php;
        context.Response.Headers["X-EcomAE-Storefront-Stub-Redirect"] = "php-canonical";
        return Task.CompletedTask;
    }
}
