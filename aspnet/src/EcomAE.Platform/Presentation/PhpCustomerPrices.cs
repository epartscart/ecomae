namespace EcomAE.Platform.Presentation;

/// <summary>
/// Published B2B price-list twin for PHP
/// <c>content/shop/catalogue/search_tabs/tabs_content/prices_download/tab_content.php</c>.
/// The CSV itself is the classic published file; this only builds the href.
/// </summary>
public static class PhpCustomerPrices
{
    public const string FilePrefix = "/content/files/Documents/prices_tmp/prices_";

    public static string FileHref(int groupId)
        => groupId <= 0
            ? string.Empty
            : FilePrefix + groupId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".csv";

    public static bool IsPublishedHref(string? href)
        => !string.IsNullOrWhiteSpace(href)
           && href.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase)
           && href.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
}
