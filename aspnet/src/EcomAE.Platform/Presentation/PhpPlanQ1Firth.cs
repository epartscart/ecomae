using System.Globalization;
using System.Net;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-firth Electronicae homepage product-line grid.
/// PHP identifier: <c>epc_el_pl_href</c>.
/// Path: <c>content/general_pages/epc_electronicae_home_product_lines.php</c>.
/// GET never mints a session cookie. Haven storefront helpers stay injected.
/// </summary>
public static class PhpPlanQ1Firth
{
    public const string ElectronicaeHomeProductLinesPath = "content/general_pages/epc_electronicae_home_product_lines.php";

    public static Func<bool>? HasPdo { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static Func<string>? SiteKey { get; set; }
    public static Func<string, List<Dictionary<string, object?>>>? ProductLineTiles { get; set; }
    public static Func<string, string>? AllLinesHref { get; set; }
    public static Func<string, string, string>? ElectronicaeHref { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        HasPdo = null;
        LangHref = null;
        SiteKey = null;
        ProductLineTiles = null;
        AllLinesHref = null;
        ElectronicaeHref = null;
        LastOutput = "";
    }

    public static string EpcElPlHref(string lang, string path)
        => H(Href(path, lang));

    public static string EpcElectronicaeHomeProductLines()
    {
        if (HasPdo?.Invoke() != true)
        {
            LastOutput = "";
            return LastOutput;
        }

        var rawLang = LangHref?.Invoke();
        var lang = rawLang is null or "" ? "/en" : rawLang;
        var site = SiteKey?.Invoke() ?? "electronicae";
        var tiles = ProductLineTiles?.Invoke(site) ?? new List<Dictionary<string, object?>>();
        if (tiles.Count == 0)
        {
            LastOutput = "";
            return LastOutput;
        }

        var cards = "";
        foreach (var tile in tiles)
        {
            var name = Str(tile.GetValueOrDefault("name"));
            var href = Coalesce(tile, "href", "/");
            var image = Str(tile.GetValueOrDefault("image"));
            var icon = Coalesce(tile, "icon", "fa-microchip");
            var accent = Coalesce(tile, "accent", "#e10a0a");

            var count = ToInt(tile.GetValueOrDefault("product_count"));
            var trend = Str(tile.GetValueOrDefault("trend"));
            var media = image != ""
                ? "\t\t\t\t\t\t\t\t\t\t<img src=\"" + H(image) + "\" alt=\"" + H(name) + "\" loading=\"lazy\" decoding=\"async\" width=\"280\" height=\"280\"/>\n"
                : "\t\t\t\t\t\t\t\t\t\t<span class=\"epc-el-pl-card__icon\" aria-hidden=\"true\"><i class=\"fa " + H(icon) + "\"></i></span>\n";
            var meta = count > 0
                ? "\t\t\t\t\t\t\t\t\t\t\t\t<span class=\"epc-el-pl-card__count\">" + count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " product" : " products") + "</span>\n"
                : "\t\t\t\t\t\t\t\t\t\t\t\t<span class=\"epc-el-pl-card__soon\">Coming soon</span>\n";
            var hot = trend == "hot"
                ? "\t\t\t\t\t\t\t\t\t\t\t\t<span class=\"epc-el-pl-card__hot\">Hot</span>\t\t\t\t\t</span>\n"
                : "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t</span>\n";
            cards +=
                "\t\t\t\t\t\t<a class=\"epc-el-pl-card\" href=\"" + EpcElPlHref(lang, href) + "\" style=\"--epc-el-accent: " + H(accent) + "\">\n" +
                "\t\t\t\t<span class=\"epc-el-pl-card__media\">\n" +
                media +
                "\t\t\t\t\t\t\t\t\t</span>\n" +
                "\t\t\t\t<span class=\"epc-el-pl-card__body\">\n" +
                "\t\t\t\t\t<span class=\"epc-el-pl-card__name\">" + H(name) + "</span>\n" +
                "\t\t\t\t\t<span class=\"epc-el-pl-card__meta\">\n" +
                meta +
                hot +
                "\t\t\t\t</span>\n" +
                "\t\t\t</a>\n";
        }

        LastOutput =
            "<section class=\"epc-el-product-lines epc-er-section epc-er-reveal\" aria-labelledby=\"epc_el_pl_title\">\n" +
            "\t<div class=\"container\">\n" +
            "\t\t<div class=\"epc-er-section__head\">\n" +
            "\t\t\t<div>\n" +
            "\t\t\t\t<h2 class=\"epc-er-section__title\" id=\"epc_el_pl_title\">Shop by product line</h2>\n" +
            "\t\t\t\t<p class=\"epc-er-section__lead\">Cell phones, laptops, TV, gaming, smart home &amp; more — verified listings with real product photos. All prices in AED.</p>\n" +
            "\t\t\t</div>\n" +
            "\t\t\t<a class=\"epc-er-section__link\" href=\"" + EpcElPlHref(lang, AllLinesHref?.Invoke(site) ?? "/catalogue/lines") + "\">View all lines</a>\n" +
            "\t\t</div>\n" +
            "\t\t<div class=\"epc-el-pl-grid\">\n" +
            cards +
            "\t\t\t\t\t</div>\n" +
            "\t</div>\n" +
            "</section>\n";
        return LastOutput;
    }

    private static string Href(string path, string lang)
    {
        if (ElectronicaeHref is not null)
        {
            return ElectronicaeHref(path, lang);
        }

        if (lang == "")
        {
            lang = "/en";
        }

        return path != "" && path[0] == '/' ? lang.TrimEnd('/') + path : path;
    }

    private static string H(string value)
        => WebUtility.HtmlEncode(value).Replace("&#39;", "&#039;", StringComparison.Ordinal);

    private static int ToInt(object? value)
    {
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string Coalesce(Dictionary<string, object?> tile, string key, string fallback)
        => tile.TryGetValue(key, out var value) && value is not null ? Str(value) : fallback;

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
