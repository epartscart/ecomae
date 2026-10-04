using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Classic-entry nginx proxies <c>/</c> to the product home. Without that proxy,
/// ASP.NET 404s the public URL. Rewrite internally so the browser URL stays
/// <c>/</c>: www.ecomae.com → marketing home, every named product tenant
/// (epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca, with or
/// without www) → the same storefront home epartscart already uses.
/// </summary>
public sealed class PublicHomeRewriteMiddleware
{
    public const string HeaderName = "X-EcomAE-Public-Home";

    private readonly RequestDelegate _next;

    public PublicHomeRewriteMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            return _next(context);
        }

        if (!TryMap(context.Request.Host.Host, context.Request.Path.Value, out var rewrite, out var kind))
        {
            return _next(context);
        }

        context.Request.Path = rewrite;
        context.Response.Headers[HeaderName] = kind;
        return _next(context);
    }

    /// <summary>
    /// Bare <c>/</c> on a product host. Industry showcase hosts
    /// (<c>{slug}.ecomae.com</c>, including <c>industries.ecomae.com</c>) are
    /// served earlier by <see cref="EcomaeIndustryShowcaseMiddleware"/>.
    /// </summary>
    public static bool TryMap(string? host, string? path, out string rewrite, out string kind)
    {
        rewrite = string.Empty;
        kind = string.Empty;
        if (path is not (null or "" or "/"))
        {
            return false;
        }

        if (StorefrontPublicSeo.IsEcomaeMarketingHost(host))
        {
            rewrite = EcomAeRoutes.MarketingApp;
            kind = "marketing";
            return true;
        }

        if (LiveTenantPresentationLock.IsLockedHost(host))
        {
            rewrite = "/storefront/app";
            kind = "storefront";
            return true;
        }

        return false;
    }
}
