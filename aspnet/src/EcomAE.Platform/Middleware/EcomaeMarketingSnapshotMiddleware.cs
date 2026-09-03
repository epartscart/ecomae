using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Serves the PHP-rendered marketing pages (snapshots of the PHP marketing router
/// output) at their canonical URLs on www.ecomae.com — /platform/*, /brochure*,
/// /documentation*, /compare*, /bos/{article}, /blockchain, /solutions*, /legal*
/// and the top-level legal aliases. 100% presentation parity with the PHP
/// reference for the whole marketing site, not only the home.
/// Runs before the admin-surface gates so public marketing articles under
/// /bos/{slug} are not blocked by the product BOS gate.
/// </summary>
public sealed class EcomaeMarketingSnapshotMiddleware
{
    private readonly RequestDelegate _next;

    public EcomaeMarketingSnapshotMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!EcomaeMarketingSnapshots.IsMarketingHost(context.Request.Host.Host))
        {
            await _next(context);
            return;
        }

        var html = EcomaeMarketingSnapshots.HtmlFor(context.Request.Path.Value);
        if (string.IsNullOrEmpty(html))
        {
            await _next(context);
            return;
        }

        var slug = EcomaeMarketingSnapshots.SlugFor(context.Request.Path.Value);
        if (slug is "platform__demo")
        {
            html = EcomaeMarketingSnapshots.ApplyDemoIndustryPref(
                html,
                context.Request.Query["industry"].ToString());
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await context.Response.WriteAsync(html, context.RequestAborted);
    }
}
