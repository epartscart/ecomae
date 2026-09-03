using EcomAE.Platform.Middleware;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP automotive home CTAs from <c>epc_asp_hero_actions</c> / <c>epc_asp_home_banners</c>
/// and <c>epart_catalog_front_links.php</c>. Lang prefix follows the request
/// (<c>/en</c>, <c>/ar</c>) so frontpage hrefs match PHP, not <c>/storefront/*-app</c>.
/// </summary>
public static class StorefrontPhpHomeLinks
{
    public static string LangHref(HttpContext? http)
    {
        var lang = LangHomeFallbackMiddleware.RequestCmsLang(http, "en");
        if (lang is not ("en" or "ar" or "me" or "ru"))
        {
            lang = "en";
        }

        return "/" + lang;
    }

    public static string Parts(string langHref) => Prefix(langHref) + "/parts";

    public static string UmapiCatalog(string langHref) => Prefix(langHref) + "/umapi_catalog";

    public static string AvailableBrands(string langHref) => Prefix(langHref) + "/available-brands";

    public static string ProductFamily(string langHref) => Prefix(langHref) + "/product-family";

    public static string VehicleCatalog(string langHref) => Prefix(langHref) + "/vehicle-catalog";

    public static string SellerRequest(string langHref) => Prefix(langHref) + "/zapros-prodavczu";

    private static string Prefix(string langHref)
    {
        var value = string.IsNullOrWhiteSpace(langHref) ? "/en" : langHref.Trim();
        return value.TrimEnd('/');
    }
}
