using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Host-gated rewrite of PHP industry slugs (<c>/gaming</c>, <c>/women</c>, <c>/gold</c>,
/// <c>/services/tax</c>, <c>/kontakty</c>) onto dedicated ASP.NET apps. Fashion
/// <c>/accessories</c> only rewrites on stylenlook — epartscart keeps auto-parts accessories.
/// Browser URL stays the PHP slug.
/// </summary>
public sealed class IndustryStorefrontSlugMiddleware
{
    public const string HeaderName = "X-EcomAE-Industry-Slug";
    public const string OriginalPathItem = "EpcIndustrySlugOriginalPath";

    private readonly RequestDelegate _next;

    public IndustryStorefrontSlugMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        var host = context.Request.Host.Host;
        if (!TryMatch(host, path, out var rewrite, out var kind))
        {
            return _next(context);
        }

        context.Items[OriginalPathItem] = path;
        context.Response.Headers[HeaderName] = kind;
        var q = rewrite.IndexOf('?', StringComparison.Ordinal);
        context.Request.Path = q < 0 ? rewrite : rewrite[..q];
        if (q >= 0 && q < rewrite.Length - 1)
        {
            context.Request.QueryString = new QueryString(rewrite[q..]);
        }

        return _next(context);
    }

    public static bool TryMatch(string? host, string path, out string rewrite, out string kind)
    {
        rewrite = string.Empty;
        kind = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var stripped = StorefrontLangPrefix.Strip(path);
        var q = stripped.IndexOf('?', StringComparison.Ordinal);
        var only = (q < 0 ? stripped : stripped[..q]).Trim('/');
        if (only.Length == 0)
        {
            return false;
        }

        var industry = StorefrontIndustryHostResolver.ResolveIndustryCode(host);
        if (only.Equals("shop/erp", StringComparison.OrdinalIgnoreCase)
            && industry is "tax_advisory")
        {
            rewrite = "/erp";
            kind = "client-erp";
            return true;
        }

        if (IsReserved(only))
        {
            return false;
        }

        if (PhpIndustryCmsPages.IsSlug(only))
        {
            rewrite = StorefrontAspNetCanonical.IndustryCms + "?slug=" + Uri.EscapeDataString(only);
            kind = "cms:" + only;
            return true;
        }

        if (PhpIndustryStorefrontCatalog.OwnsUrl(industry, only))
        {
            rewrite = StorefrontAspNetCanonical.IndustryCatalog + "?url=" + Uri.EscapeDataString(only);
            kind = "catalog:" + only;
            return true;
        }

        return false;
    }

    private static bool IsReserved(string only)
    {
        return only.StartsWith("shop/", StringComparison.OrdinalIgnoreCase)
               || only.Equals("shop", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("users/", StringComparison.OrdinalIgnoreCase)
               || only.Equals("users", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("garage", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("parts", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("storefront", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("cp", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("erp", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("bos", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("ip", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("php-reference", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("content", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("platform-assets", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("auth", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("health", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("marketing", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("accessories-spare-parts", StringComparison.OrdinalIgnoreCase)
               || only.Equals("umapi_catalog", StringComparison.OrdinalIgnoreCase)
               || only.Equals("product-family", StringComparison.OrdinalIgnoreCase)
               || only.Equals("available-brands", StringComparison.OrdinalIgnoreCase)
               || only.Equals("vehicle-catalog", StringComparison.OrdinalIgnoreCase)
               || only.Equals("katalog-laximo", StringComparison.OrdinalIgnoreCase);
    }
}
