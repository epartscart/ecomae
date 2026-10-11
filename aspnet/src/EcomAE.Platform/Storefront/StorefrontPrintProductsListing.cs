using System.Globalization;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Customer listing chrome from PHP <c>printProducts.php</c> block types 1 and 4:
/// sort and page style. Price sort is offered only when a price is sourced.
/// This is not the admin (2) or warehouse (3) editor.
/// </summary>
public static class StorefrontPrintProductsListing
{
    public const string SortNameAsc = "name_asc";
    public const string SortNameDesc = "name_desc";
    public const string SortPriceAsc = "price_asc";
    public const string SortPriceDesc = "price_desc";

    public static readonly string[] NameSorts = [SortNameAsc, SortNameDesc];
    public static readonly string[] PriceSorts = [SortPriceAsc, SortPriceDesc];

    public static string NormalizeSort(string? sort, bool priceSourced)
    {
        var value = (sort ?? string.Empty).Trim().ToLowerInvariant();
        if (priceSourced && PriceSorts.Contains(value, StringComparer.Ordinal))
        {
            return value;
        }

        return value == SortNameDesc ? SortNameDesc : SortNameAsc;
    }

    public static int NormalizeStyle(string? style)
        => style is "2" or "3" ? int.Parse(style, CultureInfo.InvariantCulture) : 1;

    public static IReadOnlyList<T> SortByName<T>(IReadOnlyList<T> rows, string sort, Func<T, string> caption)
    {
        var desc = string.Equals(NormalizeSort(sort, false), SortNameDesc, StringComparison.Ordinal);
        var ordered = rows
            .Select((row, index) => (row, index, key: caption(row) ?? string.Empty))
            .OrderBy(item => item.key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.index);
        return (desc ? ordered.Reverse() : ordered).Select(item => item.row).ToList();
    }

    public static string ListingHref(
        string basePath,
        int categoryId,
        string? categoryUrl,
        string? search,
        string sort,
        int style,
        int page = 1)
    {
        var parts = new List<string>();
        if (categoryId > 0)
        {
            parts.Add("category_id=" + categoryId.ToString(CultureInfo.InvariantCulture));
        }
        else if (!string.IsNullOrWhiteSpace(categoryUrl))
        {
            parts.Add("url=" + Uri.EscapeDataString(categoryUrl.Trim().TrimStart('/')));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            parts.Add("search_string=" + Uri.EscapeDataString(search.Trim()));
        }

        parts.Add("sort=" + Uri.EscapeDataString(NormalizeSort(sort, false)));
        parts.Add("s=" + NormalizeStyle(style.ToString(CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture));
        if (page > 1)
        {
            parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        }

        var path = string.IsNullOrWhiteSpace(basePath) ? "/storefront/own-catalog-app" : basePath;
        return path + "?" + string.Join('&', parts);
    }
}
