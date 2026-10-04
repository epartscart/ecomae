using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Classic-entry nginx proxies <c>/</c> to the product home. Without that proxy,
/// ASP.NET 404s the public URL. Rewrite internally so the browser URL stays
/// <c>/</c>: www.ecomae.com → marketing home, storefront tenants → storefront home.
/// </summary>
public sealed class PublicHomeRewriteMiddleware
{
    private readonly RequestDelegate _next;

    public PublicHomeRewriteMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            return _next(context);
        }

        var path = context.Request.Path.Value ?? "/";
        if (path is not ("/" or ""))
        {
            return _next(context);
        }

        var host = context.Request.Host.Host ?? string.Empty;
        if (StorefrontPublicSeo.IsEcomaeMarketingHost(host))
        {
            context.Request.Path = EcomAeRoutes.MarketingApp;
            context.Response.Headers["X-EcomAE-Public-Home"] = "marketing";
            return _next(context);
        }

        if (host.Contains("epartscart.com", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = "/storefront/app";
            context.Response.Headers["X-EcomAE-Public-Home"] = "storefront";
            return _next(context);
        }

        return _next(context);
    }
}
