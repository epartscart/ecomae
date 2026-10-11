using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-cape ePartsCart storefront helper. PHP identifiers kept for the inventory:
/// <c>epc_epartscart_storefront_active</c>, <c>epc_epartscart_lang_href</c>,
/// <c>epc_epartscart_is_apai_alias</c>, <c>epc_epartscart_is_apai_url</c>,
/// <c>epc_epartscart_apai_category_redirect</c>, <c>epc_epartscart_catalog_placeholder_url</c>,
/// <c>epc_epartscart_use_neutral_product_image</c>, <c>epc_storefront_catalog_placeholder_for_hint</c>,
/// <c>epc_epartscart_filter_menu_tree</c>.
/// Path: <c>content/general_pages/epc_epartscart_storefront.php</c>.
/// GET never mints a session cookie. Leftover APE storefront / categories stay injected
/// and are not written as unique leftover basenames.
/// </summary>
public static class PhpPlanQ1Cape
{
    public const string EpartscartStorefrontPath = "content/general_pages/epc_epartscart_storefront.php";

    public static Func<object?, bool>? IsWarehouseStorefront { get; set; }
    public static Func<string>? LangPrefix { get; set; }
    public static string MultilangLangHref { get; set; } = "";
    public static Func<bool>? CategoriesPresent { get; set; }
    public static Func<object?, string, string, Dictionary<string, object?>?>? ResolveProductRoute { get; set; }
    public static string ProductUrlMode { get; set; } = "alias";

    public static void Reset()
    {
        IsWarehouseStorefront = null;
        LangPrefix = null;
        MultilangLangHref = "";
        CategoriesPresent = null;
        ResolveProductRoute = null;
        ProductUrlMode = "alias";
    }

    public static bool EpcEpartscartStorefrontActive(object? pdo)
        => IsWarehouseStorefront?.Invoke(pdo) == true;

    public static string EpcEpartscartLangHref()
    {
        if (LangPrefix is not null)
        {
            return LangPrefix().TrimEnd('/');
        }

        if (!string.IsNullOrEmpty(MultilangLangHref))
        {
            return MultilangLangHref.TrimEnd('/');
        }

        return "/en";
    }

    public static bool EpcEpartscartIsApaiAlias(string alias)
    {
        alias = alias.Trim().ToLowerInvariant();
        return alias != "" && (alias.StartsWith("apai-", StringComparison.Ordinal) || alias.StartsWith("apai_", StringComparison.Ordinal));
    }

    public static bool EpcEpartscartIsApaiUrl(string urlRoute)
    {
        urlRoute = urlRoute.Trim('/');
        if (urlRoute == "")
        {
            return false;
        }

        if (urlRoute.StartsWith("apai-", StringComparison.Ordinal) || urlRoute.StartsWith("apai_", StringComparison.Ordinal))
        {
            return true;
        }

        return Regex.IsMatch(urlRoute, @"(^|/)apai[-_]");
    }

    public static string EpcEpartscartApaiCategoryRedirect(object? pdo, string urlRoute)
    {
        if (!EpcEpartscartStorefrontActive(pdo) || !EpcEpartscartIsApaiUrl(urlRoute))
        {
            return "";
        }

        if (CategoriesPresent?.Invoke() == true)
        {
            var resolved = ResolveProductRoute?.Invoke(pdo, urlRoute, ProductUrlMode);
            if (resolved is not null && !PhpEmpty(resolved.GetValueOrDefault("product")))
            {
                return "";
            }
        }

        return EpcEpartscartLangHref() + "/";
    }

    public static string EpcEpartscartCatalogPlaceholderUrl(object? pdo = null)
    {
        if (pdo is not null && EpcEpartscartStorefrontActive(pdo))
        {
            return "/content/files/images/epc_autoparts_placeholder.svg";
        }

        return "/content/files/images/no_image.png";
    }

    public static bool EpcEpartscartUseNeutralProductImage(object? pdo)
        => EpcEpartscartStorefrontActive(pdo);

    public static string EpcStorefrontCatalogPlaceholderForHint(string hint)
    {
        const string basePath = "/content/files/images/";
        var h = hint.ToLowerInvariant();
        if (h.Contains("electronic", StringComparison.Ordinal))
        {
            return basePath + "epc_electronics_placeholder.svg";
        }

        if (h.Contains("fashion", StringComparison.Ordinal) || h.Contains("apparel", StringComparison.Ordinal) || h.Contains("clothing", StringComparison.Ordinal))
        {
            return basePath + "epc_fashion_placeholder.svg";
        }

        if (h.Contains("jewel", StringComparison.Ordinal))
        {
            return basePath + "epc_jewellery_placeholder.svg";
        }

        if (h.Contains("svc-", StringComparison.Ordinal) || h.Contains("tax", StringComparison.Ordinal) || h.Contains("advisor", StringComparison.Ordinal)
            || h.Contains("audit", StringComparison.Ordinal) || h.Contains("accounting", StringComparison.Ordinal) || h.Contains("consult", StringComparison.Ordinal)
            || h.Contains("service", StringComparison.Ordinal))
        {
            return basePath + "epc_services_placeholder.svg";
        }

        if (h.Contains("auto", StringComparison.Ordinal) || h.Contains("part", StringComparison.Ordinal))
        {
            return basePath + "epc_autoparts_placeholder.svg";
        }

        return basePath + "epc_catalog_placeholder.svg";
    }

    public static List<Dictionary<string, object?>> EpcEpartscartFilterMenuTree(object? pdo, List<Dictionary<string, object?>> tree)
    {
        if (!EpcEpartscartStorefrontActive(pdo))
        {
            return tree;
        }

        var output = new List<Dictionary<string, object?>>();
        foreach (var node in tree)
        {
            var copy = new Dictionary<string, object?>(node, StringComparer.Ordinal);
            var alias = Convert.ToString(copy.GetValueOrDefault("alias")) ?? "";
            if (EpcEpartscartIsApaiAlias(alias))
            {
                continue;
            }

            if (copy.GetValueOrDefault("data") is List<Dictionary<string, object?>> children && children.Count > 0)
            {
                copy["data"] = EpcEpartscartFilterMenuTree(pdo, children);
            }

            output.Add(copy);
        }

        return output;
    }

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or "0" or 0 or 0L or 0.0;
}
