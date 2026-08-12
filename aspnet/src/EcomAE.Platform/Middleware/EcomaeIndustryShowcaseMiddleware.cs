using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Serves PHP-parity industry showcase HTML on <c>{slug}.ecomae.com</c>
/// (hub + sub-industry paths). Must run before storefront Blazor so classic-entry
/// remaps of <c>/</c> → <c>/storefront/app</c> still show the industry template.
/// </summary>
public sealed class EcomaeIndustryShowcaseMiddleware
{
    private readonly RequestDelegate _next;

    public EcomaeIndustryShowcaseMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var html = EcomaeIndustryShowcaseSnapshots.HtmlFor(
            context.Request.Host.Host,
            context.Request.Path.Value);
        if (string.IsNullOrEmpty(html))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-EcomAE-Industry-Showcase"] = "snapshot";
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await context.Response.WriteAsync(html, context.RequestAborted);
    }
}
