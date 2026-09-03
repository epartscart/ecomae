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

    /// <summary>
    /// Package header + CSS + footer around an inner page (category / CMS),
    /// same chrome PHP <c>templates/nero/desktop.php</c> uses for custom packages.
    /// </summary>
    public static string WrapInner(string package, string innerHtml)
        => TrySplitChrome(package, out var header, out var footer)
            ? header + (innerHtml ?? string.Empty) + footer
            : (innerHtml ?? string.Empty);

    public static bool TrySplitChrome(string package, out string header, out string footer)
    {
        header = string.Empty;
        footer = string.Empty;
        var snapshot = HtmlFor(package);
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        var homeClass = package.ToLowerInvariant() switch
        {
            "electronics_retail_virgin" => "epc-er-home",
            "fashion_retail_namshi" => "epc-frn-home",
            "jewellery_retail_kiyasha" => "epc-jrk-home",
            "consulting_primeinvest" => "epc-cpi-home",
            _ => string.Empty,
        };
        if (homeClass.Length == 0)
        {
            return false;
        }

        var homeNeedle = "class=\"" + homeClass;
        var homeIdx = snapshot.IndexOf(homeNeedle, StringComparison.OrdinalIgnoreCase);
        if (homeIdx < 0)
        {
            return false;
        }

        var openDiv = snapshot.LastIndexOf("<div", homeIdx, StringComparison.OrdinalIgnoreCase);
        if (openDiv < 0)
        {
            openDiv = homeIdx;
        }

        var newsIdx = snapshot.IndexOf("<section class=\"epc-wc-newsletter", openDiv, StringComparison.OrdinalIgnoreCase);
        var footerIdx = snapshot.IndexOf("<footer", openDiv, StringComparison.OrdinalIgnoreCase);
        var tailIdx = newsIdx >= 0 ? newsIdx : footerIdx;
        if (tailIdx < 0)
        {
            return false;
        }

        header = snapshot[..openDiv];
        footer = snapshot[tailIdx..];
        return header.Length > 0 && footer.Length > 0;
    }
}
