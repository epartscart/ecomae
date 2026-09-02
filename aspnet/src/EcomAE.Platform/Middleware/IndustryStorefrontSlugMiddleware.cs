using EcomAE.Platform.Migration;
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
        var package = StorefrontIndustryHostResolver.ResolveStorefrontPackage(industry);
        var incomingQuery = q >= 0 ? stripped[q..] : string.Empty;

        if (only.Equals("shop/erp", StringComparison.OrdinalIgnoreCase)
            && industry is "tax_advisory")
        {
            rewrite = "/erp";
            kind = "client-erp";
            return true;
        }

        if (only.Equals("shop/search", StringComparison.OrdinalIgnoreCase)
            && PhpTenantHomeSnapshots.IsCustomPackage(package))
        {
            rewrite = StorefrontAspNetCanonical.IndustrySearch + incomingQuery;
            kind = "industry-search";
            return true;
        }

        if (only.Equals("vendor", StringComparison.OrdinalIgnoreCase)
            || only.Equals("vendor/login", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.VendorPortal;
            kind = "vendor";
            return true;
        }

        if (only.Equals("vendor/register", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.VendorRegister;
            kind = "vendor-register";
            return true;
        }

        if (only.Equals("vendor/upload", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.VendorUpload;
            kind = "vendor-upload";
            return true;
        }

        if (only.Equals("users/forgot", StringComparison.OrdinalIgnoreCase)
            || only.Equals("users/forgot_password", StringComparison.OrdinalIgnoreCase)
            || only.Equals("users/new_password", StringComparison.OrdinalIgnoreCase)
            || only.Equals("users/password-reset", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.ForgotPassword + incomingQuery;
            kind = "forgot-password";
            return true;
        }

        if (only.Equals("users/confirm", StringComparison.OrdinalIgnoreCase)
            || only.Equals("users/confirm_contact", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.ConfirmContact + incomingQuery;
            kind = "confirm-contact";
            return true;
        }

        if (only.Equals("shop/returns", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("shop/returns/", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.CustomerReturns + incomingQuery;
            kind = "customer-returns";
            return true;
        }

        if (only.Equals("zapros-prodavczu", StringComparison.OrdinalIgnoreCase)
            || ((only.Equals("vin-zapros", StringComparison.OrdinalIgnoreCase)
                 || only.Equals("vin_zapros", StringComparison.OrdinalIgnoreCase))
                && industry is "auto_parts"))
        {
            rewrite = StorefrontAspNetCanonical.SellerRequest + incomingQuery;
            kind = "seller-request";
            return true;
        }

        if (only.Equals("requests", StringComparison.OrdinalIgnoreCase)
            || only.Equals("requests/request", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("requests/", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.CustomerRequests + incomingQuery;
            kind = "customer-requests";
            return true;
        }

        if (only.Equals("shop/print", StringComparison.OrdinalIgnoreCase)
            || only.Equals("shop/print_docs", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("shop/print/", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("shop/print_docs/", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.CustomerPrint + incomingQuery;
            kind = "customer-print";
            return true;
        }

        if (only.Equals("shop/orders/guest", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.GuestOrder + incomingQuery;
            kind = "guest-order";
            return true;
        }

        if (only.Equals("shop/pay", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("shop/pay/", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.Payment + incomingQuery;
            kind = "customer-pay";
            return true;
        }

        if (only.Equals("sitemap", StringComparison.OrdinalIgnoreCase)
            || only.Equals("shop/sitemap", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.Sitemap + incomingQuery;
            kind = "sitemap";
            return true;
        }

        if (only.Equals("brochure", StringComparison.OrdinalIgnoreCase)
            && LiveTenantPresentationLock.IsProductTenantHost(host))
        {
            rewrite = StorefrontAspNetCanonical.Brochure + incomingQuery;
            kind = "brochure";
            return true;
        }

        if (only.Equals("ofisy", StringComparison.OrdinalIgnoreCase)
            || only.Equals("shop/offices", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.Offices + incomingQuery;
            kind = "offices";
            return true;
        }

        if (only.Equals("ai-parts-expert", StringComparison.OrdinalIgnoreCase)
            && industry is "auto_parts")
        {
            rewrite = StorefrontAspNetCanonical.AiPartsExpert + incomingQuery;
            kind = "ai-parts-expert";
            return true;
        }

        if (industry is "auto_parts"
            && (only.Equals("shop/katalogi-ucats", StringComparison.OrdinalIgnoreCase)
                || only.StartsWith("shop/katalogi-ucats/", StringComparison.OrdinalIgnoreCase)
                || only.Equals("shop/ucats", StringComparison.OrdinalIgnoreCase)
                || only.StartsWith("shop/ucats/", StringComparison.OrdinalIgnoreCase)))
        {
            var ucatsKey = only.Contains('/', StringComparison.Ordinal)
                ? only[(only.LastIndexOf('/', StringComparison.Ordinal) + 1)..]
                : string.Empty;
            if (ucatsKey.Equals("katalogi-ucats", StringComparison.OrdinalIgnoreCase)
                || ucatsKey.Equals("ucats", StringComparison.OrdinalIgnoreCase)
                || ucatsKey.Equals("catalogues", StringComparison.OrdinalIgnoreCase)
                || ucatsKey.Length == 0)
            {
                rewrite = StorefrontAspNetCanonical.UcatsService + incomingQuery;
                kind = "ucats";
                return true;
            }

            var card = StorefrontUcatsCatalog.Find(ucatsKey);
            rewrite = card is null
                ? StorefrontAspNetCanonical.UcatsService + incomingQuery
                : StorefrontAspNetCanonical.UcatsService + "/" + card.Slug + incomingQuery;
            kind = "ucats";
            return true;
        }

        if ((only.Equals("vin", StringComparison.OrdinalIgnoreCase)
             || only.Equals("katalog-laximo", StringComparison.OrdinalIgnoreCase))
            && industry is "auto_parts")
        {
            rewrite = StorefrontAspNetCanonical.LaximoVin + incomingQuery;
            kind = "laximo-vin";
            return true;
        }

        if (industry is "auto_parts"
            && PhpSpecialSearches.TryFind(only, out _))
        {
            var alias = PhpSpecialSearches.Normalize(only);
            rewrite = StorefrontAspNetCanonical.SpecialSearch + "?alias=" + Uri.EscapeDataString(alias);
            kind = "special-search";
            return true;
        }

        if (only.Equals("shop/catalogue", StringComparison.OrdinalIgnoreCase)
            || only.Equals("katalog", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("shop/catalogue/", StringComparison.OrdinalIgnoreCase))
        {
            if (only.Contains("product", StringComparison.OrdinalIgnoreCase))
            {
                rewrite = StorefrontAspNetCanonical.Product + incomingQuery;
                kind = "catalogue-product";
                return true;
            }

            rewrite = StorefrontAspNetCanonical.OwnCatalog + incomingQuery;
            kind = "own-catalog";
            return true;
        }

        if (only.Equals("shop/product", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.Product + incomingQuery;
            kind = "catalogue-product";
            return true;
        }

        if (PhpStorefrontNews.IsNewsPath(only))
        {
            var newsQuery = incomingQuery;
            if (only.Contains('/', StringComparison.Ordinal)
                && !newsQuery.Contains("url=", StringComparison.OrdinalIgnoreCase))
            {
                newsQuery = "?url=" + Uri.EscapeDataString(only)
                            + (incomingQuery.Length > 0 && incomingQuery[0] == '?'
                                ? "&" + incomingQuery[1..]
                                : incomingQuery);
            }

            rewrite = StorefrontAspNetCanonical.News + newsQuery;
            kind = "news";
            return true;
        }

        if (only.Equals("auto-workshop", StringComparison.OrdinalIgnoreCase)
            && industry is "auto_parts")
        {
            rewrite = StorefrontAspNetCanonical.AutoWorkshop + incomingQuery;
            kind = "auto-workshop";
            return true;
        }

        if ((only.Equals("garage/manager", StringComparison.OrdinalIgnoreCase)
             || only.Equals("garage/manager/", StringComparison.OrdinalIgnoreCase))
            && industry is "auto_parts")
        {
            rewrite = StorefrontAspNetCanonical.GarageManager;
            kind = "garage-manager";
            return true;
        }

        if (only.Equals("garazh", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("garazh/", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.GarageLogin + incomingQuery;
            kind = "customer-garage";
            return true;
        }

        if (only.Equals("newsletter", StringComparison.OrdinalIgnoreCase)
            || only.Equals("subscribe", StringComparison.OrdinalIgnoreCase))
        {
            rewrite = StorefrontAspNetCanonical.Newsletter;
            kind = "newsletter";
            return true;
        }

        if (only.StartsWith("p/", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("product/", StringComparison.OrdinalIgnoreCase))
        {
            var alias = only.Contains('/', StringComparison.Ordinal)
                ? only[(only.IndexOf('/', StringComparison.Ordinal) + 1)..]
                : string.Empty;
            if (PhpIndustryStorefrontCatalog.TryFindProduct(industry, alias, out _))
            {
                rewrite = StorefrontAspNetCanonical.IndustryProduct + "?sku=" + Uri.EscapeDataString(alias);
                kind = "product:" + alias;
                return true;
            }
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

        if (industry is "auto_parts" && PhpOwnCatalogSlugs.IsAlias(only))
        {
            rewrite = StorefrontAspNetCanonical.OwnCatalog + "?url=" + Uri.EscapeDataString(PhpOwnCatalogSlugs.Normalize(only));
            kind = "own-catalog-slug";
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
               || only.Equals("katalog-laximo", StringComparison.OrdinalIgnoreCase)
               || only.Equals("zapros-prodavczu", StringComparison.OrdinalIgnoreCase)
               || only.Equals("requests", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("requests/", StringComparison.OrdinalIgnoreCase)
               || only.Equals("novosti", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("novosti/", StringComparison.OrdinalIgnoreCase)
               || only.Equals("news", StringComparison.OrdinalIgnoreCase)
               || only.StartsWith("news/", StringComparison.OrdinalIgnoreCase)
               || only.Equals("sitemap", StringComparison.OrdinalIgnoreCase)
               || only.Equals("katalog", StringComparison.OrdinalIgnoreCase)
               || only.Equals("brochure", StringComparison.OrdinalIgnoreCase)
               || only.Equals("ofisy", StringComparison.OrdinalIgnoreCase)
               || only.Equals("ai-parts-expert", StringComparison.OrdinalIgnoreCase)
               || only.Equals("vin", StringComparison.OrdinalIgnoreCase)
               || only.Equals("vin-zapros", StringComparison.OrdinalIgnoreCase)
               || only.Equals("vin_zapros", StringComparison.OrdinalIgnoreCase);
    }
}
