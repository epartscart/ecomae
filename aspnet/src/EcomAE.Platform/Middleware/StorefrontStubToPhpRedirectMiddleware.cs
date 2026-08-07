using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Legacy: thin /storefront stubs once redirected to interim PHP <c>/en/*</c> pages.
/// Product mode (<see cref="StorefrontSurfaceLinks.PreferAspNetApps"/>, default on) keeps
/// traffic on ASP.NET apps — PHP compare is <c>/php-reference/*</c> only.
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
        if (StorefrontSurfaceLinks.PreferAspNetApps)
        {
            context.Response.Headers["X-EcomAE-Storefront-Stub-Redirect"] = "skipped-aspnet-primary";
            return _next(context);
        }

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
