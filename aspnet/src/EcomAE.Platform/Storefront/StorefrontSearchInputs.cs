using System.Text.RegularExpressions;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/docpart/search_strs_for_inputs.php</c>: the values put into the article and name-search
/// inputs on page load. The article keeps only latin, digits and Cyrillic (including yo); the name string is
/// <c>htmlentities</c>. GET beats <c>$DP_Content->service_data["article"]</c>. Verified against PHP 8.3 by
/// <c>Fixtures/StorefrontFragments/golden.json</c>.
/// </summary>
public static partial class StorefrontSearchInputs
{
    [GeneratedRegex(@"[^A-Za-z0-9А-Яа-яёЁ]", RegexOptions.CultureInvariant)]
    private static partial Regex NotArticle();

    public sealed record Values(string Article, string SearchString);

    public static Values From(IReadOnlyDictionary<string, string?> get, IReadOnlyDictionary<string, string?>? serviceData)
    {
        var article = string.Empty;
        if (get.ContainsKey("article"))
        {
            article = CleanArticle(get["article"]);
        }
        else if (serviceData is not null && serviceData.TryGetValue("article", out var serviceArticle))
        {
            article = CleanArticle(serviceArticle);
        }

        var search = get.ContainsKey("search_string")
            ? PhpHtmlEntities.Encode(get["search_string"] ?? string.Empty)
            : string.Empty;
        return new Values(article, search);
    }

    public static string CleanArticle(string? raw) => NotArticle().Replace(raw ?? string.Empty, string.Empty);
}
