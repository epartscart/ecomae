using System.Collections;
using System.Globalization;
using System.Net;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-creek ePartsCart homepage product-line grid.
/// PHP identifiers: <c>epc_ep_pl_href</c>, <c>epc_perf_cache_get</c>.
/// Path: <c>content/general_pages/epc_epartscart_home_product_lines.php</c>.
/// GET never mints a session cookie. Haven storefront helpers stay injected.
/// </summary>
public static class PhpPlanQ1Creek
{
    public const string EpartscartHomeProductLinesPath = "content/general_pages/epc_epartscart_home_product_lines.php";
    public const string SiteKey = "epartscart";

    public static Func<bool>? HasPdo { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static Func<string, object?>? PerfCacheGet { get; set; }
    public static Func<object?>? HomePerfFast { get; set; }
    public static Func<string, List<Dictionary<string, object?>>>? ProductLineTiles { get; set; }
    public static Func<string, string>? AllLinesHref { get; set; }
    public static Func<string, string, string>? ElectronicaeHref { get; set; }
    public static string LastCacheKey { get; private set; } = "";
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        HasPdo = null;
        LangHref = null;
        PerfCacheGet = null;
        HomePerfFast = null;
        ProductLineTiles = null;
        AllLinesHref = null;
        ElectronicaeHref = null;
        LastCacheKey = "";
        LastOutput = "";
    }

    public static string EpcEpPlHref(string lang, string path)
        => H(Href(path, lang));

    public static object? EpcPerfCacheGet(string key)
        => PerfCacheGet?.Invoke(key);

    public static string EpcEpartscartHomeProductLines()
    {
        if (HasPdo?.Invoke() != true)
        {
            LastOutput = "";
            return LastOutput;
        }

        LastCacheKey = "epc_pl_tiles:v1:" + SiteKey + ":8";
        var cached = EpcPerfCacheGet(LastCacheKey);
        if (!PhpEmpty(HomePerfFast?.Invoke()) && cached is null)
        {
            LastOutput = "";
            return LastOutput;
        }

        var tiles = cached is IEnumerable and not string
            ? ToTiles(cached)
            : ProductLineTiles?.Invoke(SiteKey) ?? new List<Dictionary<string, object?>>();
        if (tiles.Count == 0)
        {
            LastOutput = "";
            return LastOutput;
        }

        var rawLang = LangHref?.Invoke();
        var lang = rawLang is null or "" ? "/en" : rawLang;
        var cards = "";
        foreach (var tile in tiles)
        {
            var name = Str(tile.GetValueOrDefault("name"));
            var href = Coalesce(tile, "href", "/");
            var image = Str(tile.GetValueOrDefault("image"));
            var icon = Coalesce(tile, "icon", "fa-cogs");
            var count = ToInt(tile.GetValueOrDefault("product_count"));
            var media = image != ""
                ? "\t\t\t\t\t\t\t\t\t\t<img src=\"" + H(image) + "\" alt=\"" + H(name) + "\" loading=\"lazy\" decoding=\"async\" width=\"220\" height=\"220\"/>\n"
                : "\t\t\t\t\t\t\t\t\t\t<span class=\"epc-ep-pl-card__icon\" aria-hidden=\"true\"><i class=\"fa " + H(icon) + "\"></i></span>\n";
            var meta = count > 0
                ? count.ToString(CultureInfo.InvariantCulture) + " products"
                : "New arrivals soon";
            cards +=
                "\t\t\t\t\t\t<a class=\"epc-ep-pl-card\" href=\"" + EpcEpPlHref(lang, href) + "\">\n" +
                "\t\t\t\t<span class=\"epc-ep-pl-card__media\">\n" +
                media +
                "\t\t\t\t\t\t\t\t\t</span>\n" +
                "\t\t\t\t<span class=\"epc-ep-pl-card__body\">\n" +
                "\t\t\t\t\t<span class=\"epc-ep-pl-card__name\">" + H(name) + "</span>\n" +
                "\t\t\t\t\t<span class=\"epc-ep-pl-card__meta\">" + meta + "</span>\n" +
                "\t\t\t\t</span>\n" +
                "\t\t\t</a>\n";
        }

        LastOutput =
            "<section class=\"epc-ep-product-lines epc-asp-home-section\" aria-labelledby=\"epc_ep_pl_title\">\n" +
            "\t<div class=\"container\">\n" +
            "\t\t<div class=\"epc-asp-home-section__head\">\n" +
            "\t\t\t<h2 class=\"epc-asp-home-section__title\" id=\"epc_ep_pl_title\">Shop by product line</h2>\n" +
            "\t\t\t<p class=\"epc-asp-home-section__lead\">Engine, brakes, body, interior — verified UAE listings imported via Auto Price AI.</p>\n" +
            "\t\t\t<a class=\"epc-asp-home-section__link\" href=\"" + EpcEpPlHref(lang, AllLinesHref?.Invoke(SiteKey) ?? "/catalogue/lines") + "\">View all lines</a>\n" +
            "\t\t</div>\n" +
            "\t\t<div class=\"epc-ep-pl-grid\">\n" +
            cards +
            "\t\t\t\t\t</div>\n" +
            "\t</div>\n" +
            "</section>\n" +
            "<style>\n" +
            ".epc-ep-pl-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:14px;margin-top:16px}\n" +
            ".epc-ep-pl-card{display:flex;flex-direction:column;background:#fff;border:1px solid #e5eaf2;border-radius:14px;overflow:hidden;text-decoration:none;color:inherit;box-shadow:0 10px 28px rgba(15,23,42,.08)}\n" +
            ".epc-ep-pl-card__media{display:flex;align-items:center;justify-content:center;min-height:120px;background:#f8fafc}\n" +
            ".epc-ep-pl-card__media img{max-width:100%;max-height:120px;object-fit:contain}\n" +
            ".epc-ep-pl-card__icon{font-size:32px;color:#dc2626}\n" +
            ".epc-ep-pl-card__body{padding:12px 14px}\n" +
            ".epc-ep-pl-card__name{display:block;font-weight:800;font-size:14px}\n" +
            ".epc-ep-pl-card__meta{display:block;font-size:12px;color:#64748b;margin-top:4px}\n" +
            ".epc-asp-home-section{margin:28px 0}\n" +
            ".epc-asp-home-section__head{display:flex;flex-wrap:wrap;align-items:flex-end;justify-content:space-between;gap:10px}\n" +
            ".epc-asp-home-section__title{margin:0;font-size:22px;font-weight:900}\n" +
            ".epc-asp-home-section__lead{margin:6px 0 0;color:#64748b;flex:1 1 100%}\n" +
            ".epc-asp-home-section__link{font-weight:700;color:#dc2626}\n" +
            "</style>\n";
        return LastOutput;
    }

    private static List<Dictionary<string, object?>> ToTiles(object cached)
    {
        if (cached is List<Dictionary<string, object?>> list)
        {
            return list;
        }

        var tiles = new List<Dictionary<string, object?>>();
        if (cached is IEnumerable rows)
        {
            foreach (var row in rows)
            {
                if (row is Dictionary<string, object?> dict)
                {
                    tiles.Add(dict);
                }
            }
        }

        return tiles;
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

    private static bool PhpEmpty(object? value)
    {
        if (value is null or false or "")
        {
            return true;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (text is null or "" or "0")
        {
            return true;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) == 0;
        }
        catch
        {
            return false;
        }
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
