using System.Globalization;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Customer listing from PHP <c>content/shop/catalogue/printProducts.php</c> and
/// <c>content/shop/catalogue/printProducts_2.php</c> (block type 1). Child tiles are
/// <c>content/shop/catalogue/printCategories.php</c> type 1. Style cookie is
/// <c>content/shop/catalogue/set_cookie_products_style.php</c>. Name search is
/// <c>content/shop/catalogue/text_search_algorithm.php</c> (tokens ≥ 2, caption +
/// description LIKE, article alias) — same algorithm as <c>SearchIdsAsync</c>.
/// Admin type 2 and warehouse type 3 stay on the Control Panel catalogue.
/// </summary>
public static class StorefrontPrintProducts
{
    public const string PrintProductsPath = "content/shop/catalogue/printProducts.php";
    public const string PrintProducts2Path = "content/shop/catalogue/printProducts_2.php";
    public const string PrintCategoriesPath = "content/shop/catalogue/printCategories.php";
    public const string StyleCookiePath = "content/shop/catalogue/set_cookie_products_style.php";
    public const string TextSearchAlgorithmPath = "content/shop/catalogue/text_search_algorithm.php";
    public const string StyleCookieName = "products_style";
    public const int CustomerBlockType = 1;
    public const int AdminBlockType = 2;
    public const int WarehouseBlockType = 3;
    public const int PageSize = 12;

    // printProducts.php
    public const string write_log_txt = nameof(write_log_txt);
    public const string initProperiesValues = nameof(initProperiesValues);
    public const string productsCountRequest = nameof(productsCountRequest);
    public const string URL_Filters_to_url = nameof(URL_Filters_to_url);
    public const string URL_Filters_from_url = nameof(URL_Filters_from_url);
    public const string getProductsHTML = nameof(getProductsHTML);
    public const string onStartShow = nameof(onStartShow);
    public const string onNewPropertiesShow = nameof(onNewPropertiesShow);
    public const string getNextPage = nameof(getNextPage);
    public const string showRestyle = nameof(showRestyle);
    public const string setSortModeInRequestObject = nameof(setSortModeInRequestObject);
    public const string showResort = nameof(showResort);
    public const string showPropertiesWidgets = nameof(showPropertiesWidgets);
    public const string onTreeListSelectChange = nameof(onTreeListSelectChange);
    public const string sliderIntFloatInit = nameof(sliderIntFloatInit);
    public const string other_list_options_handle = nameof(other_list_options_handle);
    public const string showProductsCount = nameof(showProductsCount);
    public const string hideProductsCount = nameof(hideProductsCount);
    public const string setProductsCountPopupId = nameof(setProductsCountPopupId);
    public const string on_name_filter_button = nameof(on_name_filter_button);
    public const string on_product_name_search_str_entered = nameof(on_product_name_search_str_entered);

    // printProducts_2.php extras
    public const string str_replace_once_all = nameof(str_replace_once_all);
    public const string show_pagination = nameof(show_pagination);
    public const string show_more_click = nameof(show_more_click);
    public const string show_more_btn = nameof(show_more_btn);
    public const string AJAXgetProductsHTML = nameof(AJAXgetProductsHTML);
    public const string onchange_range_min = nameof(onchange_range_min);
    public const string onchange_range_max = nameof(onchange_range_max);
    public const string proverka_numeric = nameof(proverka_numeric);
    public const string show_filter_box = nameof(show_filter_box);
    public const string filter_properties_close = nameof(filter_properties_close);

    public static int NormalizePage(string? page)
        => int.TryParse(page, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n
            : 1;

    public static int LoadLimit(int page)
        => Math.Clamp(Math.Max(1, page) * PageSize, PageSize, 200);

    public static int ResolveStyle(string? queryStyle, string? cookieStyle)
    {
        if (TryStyleCookie(queryStyle, out var fromQuery))
        {
            return fromQuery;
        }

        return TryStyleCookie(cookieStyle, out var fromCookie) ? fromCookie : 1;
    }

    public static bool TryStyleCookie(string? raw, out int style)
    {
        style = 0;
        if (!int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            || n is not (1 or 2 or 3))
        {
            return false;
        }

        style = n;
        return true;
    }

    public static string StyleCookieJson(int style)
        => style.ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<StorefrontCatalogueCategoryNode> CustomerCategoryTiles(
        IReadOnlyList<StorefrontCatalogueCategoryNode> tree,
        int categoryId,
        string? categoryUrl)
    {
        if (tree.Count == 0)
        {
            return [];
        }

        if (categoryId <= 0 && string.IsNullOrWhiteSpace(categoryUrl))
        {
            return tree;
        }

        var node = FindNode(tree, categoryId, categoryUrl);
        return node?.Data ?? [];
    }

    public static StorefrontCatalogueCategoryNode? FindNode(
        IReadOnlyList<StorefrontCatalogueCategoryNode> tree,
        int categoryId,
        string? categoryUrl)
    {
        var url = (categoryUrl ?? string.Empty).Trim().TrimStart('/');
        foreach (var node in tree)
        {
            if ((categoryId > 0 && node.Id == categoryId)
                || (url.Length > 0 && string.Equals(url, node.Url.Trim().TrimStart('/'), StringComparison.OrdinalIgnoreCase)))
            {
                return node;
            }

            var child = FindNode(node.Data, categoryId, categoryUrl);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    public static IReadOnlyList<PagerLink> Pagination(string listingHrefWithoutPage, int page, int loadedCount, bool hasMore)
    {
        var current = NormalizePage(page.ToString(CultureInfo.InvariantCulture));
        var pages = hasMore ? current + 1 : current;
        if (loadedCount == 0)
        {
            pages = 1;
        }

        var links = new List<PagerLink>
        {
            new("First", PageHref(listingHrefWithoutPage, 1), current == 1, current == 1),
            new("Prev", PageHref(listingHrefWithoutPage, Math.Max(1, current - 1)), false, current == 1),
        };

        var from = Math.Max(1, current - 2);
        var to = Math.Min(pages, current + 2);
        for (var i = from; i <= to; i++)
        {
            links.Add(new(i.ToString(CultureInfo.InvariantCulture), PageHref(listingHrefWithoutPage, i), i == current, false));
        }

        links.Add(new("Next", PageHref(listingHrefWithoutPage, current + 1), false, !hasMore && current >= pages));
        links.Add(new("Last", PageHref(listingHrefWithoutPage, pages), false, !hasMore && current >= pages));
        return links;
    }

    public static string PageHref(string listingHrefWithoutPage, int page)
    {
        var href = listingHrefWithoutPage ?? string.Empty;
        var sep = href.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return href + sep + "page=" + NormalizePage(page.ToString(CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
    }

    public static string FiltersToUrl(int categoryId, string? categoryUrl, string? search, string sort, int style, int page)
        => StorefrontPrintProductsListing.ListingHref(
            "/storefront/own-catalog-app",
            categoryId,
            categoryUrl,
            search,
            sort,
            style,
            page);

    public sealed record PagerLink(string Label, string Href, bool Active, bool Disabled);
}
