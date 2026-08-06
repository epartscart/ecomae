namespace EcomAE.Platform.Presentation;

/// <summary>
/// Serves the PHP-rendered custom-storefront tenant homes (header + home +
/// newsletter + footer) captured by <c>scripts/render_php_home_snapshots.php</c>.
/// Same-to-same with what templates/nero/desktop.php emits on each tenant host:
/// electronicae (Virgin electronics), stylenlook (Namshi fashion),
/// thejewellerytrend (Kiyasha jewellery), taxofinca (Prime Invest consulting).
/// </summary>
public static class PhpTenantHomeSnapshots
{
    private static readonly string[] KnownPackages =
    [
        "electronics_retail_virgin",
        "fashion_retail_namshi",
        "jewellery_retail_kiyasha",
        "consulting_primeinvest",
    ];

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DateTime StampUtc, string Html)> Cache = new(StringComparer.Ordinal);

    public static bool IsCustomPackage(string? package) =>
        !string.IsNullOrWhiteSpace(package) && KnownPackages.Contains(package, StringComparer.OrdinalIgnoreCase);

    /// <summary>Empty string when the snapshot is unavailable — caller shows a fallback.</summary>
    public static string HtmlFor(string package)
    {
        if (!IsCustomPackage(package))
        {
            return string.Empty;
        }

        var relative = "content/general_pages/epc_rendered_homes/" + package.ToLowerInvariant() + ".html";
        return PhpHomeWidgetHtml.RenderStatic(relative);
    }
}
