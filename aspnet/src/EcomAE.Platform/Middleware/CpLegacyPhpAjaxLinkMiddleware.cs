using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Rewrites tenant Control Panel ajax posts onto the existing procurement and ERP engines
/// before the product-path redirect can turn them into a browse page.
/// </summary>
public sealed class CpLegacyPhpAjaxLinkMiddleware
{
    private readonly RequestDelegate _next;

    public CpLegacyPhpAjaxLinkMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (CpLegacyPhpAjaxLinks.TryRewritePost(context.Request.Method, context.Request.Path, out var target))
        {
            context.Request.Path = target;
        }

        return _next(context);
    }
}
