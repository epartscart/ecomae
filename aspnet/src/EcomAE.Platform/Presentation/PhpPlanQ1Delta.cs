using System.Globalization;
using System.Net;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-delta ePartsCart homepage new-arrivals grid.
/// Path: <c>content/general_pages/epc_epartscart_home_new_arrivals.php</c>.
/// GET never mints a session cookie. Haven storefront helpers stay injected.
/// Leftover unique Auto Price parse stays injected and is not named here.
/// </summary>
public static class PhpPlanQ1Delta
{
    public const string EpartscartHomeNewArrivalsPath = "content/general_pages/epc_epartscart_home_new_arrivals.php";
    public const string SiteKey = "epartscart";

    public static Func<bool>? HasPdo { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static string ProductUrlMode { get; set; } = "alias";
    public static Func<List<Dictionary<string, object?>>>? QueryImported { get; set; }
    public static Func<Dictionary<string, object?>, string, Dictionary<string, object?>>? ProductCard { get; set; }
    public static Func<string, Dictionary<string, string>>? ParseChpu { get; set; }
    public static Func<string, string, string>? ElectronicaeHref { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        HasPdo = null;
        LangHref = null;
        ProductUrlMode = "alias";
        QueryImported = null;
        ProductCard = null;
        ParseChpu = null;
        ElectronicaeHref = null;
        LastOutput = "";
    }

    public static string EpcEpartscartHomeNewArrivals()
    {
        if (HasPdo?.Invoke() != true)
        {
            LastOutput = "";
            return LastOutput;
        }

        var rows = QueryImported?.Invoke() ?? new List<Dictionary<string, object?>>();
        var products = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var card = ProductCard?.Invoke(row, ProductUrlMode) ?? DefaultCard(row, ProductUrlMode);
            if (Str(card.GetValueOrDefault("href")) == "" || Str(card.GetValueOrDefault("name")) == "")
            {
                continue;
            }

            var parsed = ParseChpu?.Invoke(Str(row.GetValueOrDefault("alias")))
                ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "", ["article"] = "" };
            if (!PhpEmpty(parsed.GetValueOrDefault("brand")))
            {
                card["brand"] = parsed["brand"].ToUpperInvariant();
            }

            if (!PhpEmpty(parsed.GetValueOrDefault("article")))
            {
                card["sku"] = parsed["article"];
            }

            products.Add(card);
        }

        if (products.Count == 0)
        {
            LastOutput = "";
            return LastOutput;
        }

        var rawLang = LangHref?.Invoke();
        var lang = rawLang is null or "" ? "/en" : rawLang;
        var cards = "";
        foreach (var product in products)
        {
            var href = Href(Coalesce(product, "href", "/"), lang);
            var sku = product.TryGetValue("sku", out var skuRaw) && skuRaw is not null ? Str(skuRaw) : "";
            var label = (Coalesce(product, "brand", "") + (sku != "" ? " · " + sku : "")).Trim();
            var image = Str(product.GetValueOrDefault("image"));
            var name = Str(product.GetValueOrDefault("name"));
            var price = ToFloat(product.GetValueOrDefault("price"));
            cards += "\t\t\t\t\t\t<a class=\"epc-ep-na-card\" href=\"" + H(href) + "\">\n";
            if (!PhpEmpty(image))
            {
                cards += "\t\t\t\t\t\t\t\t<img src=\"" + H(image) + "\" alt=\"\" loading=\"lazy\" decoding=\"async\"/>\n";
            }

            cards += "\t\t\t\t\t\t\t\t<span class=\"epc-ep-na-card__body\">\n";
            if (label != "")
            {
                cards += "\t\t\t\t\t<strong class=\"epc-ep-na-card__id\">" + H(label) + "</strong>\t\t\t\t\t<span class=\"epc-ep-na-card__title\">" + H(name) + "</span>\n";
            }
            else
            {
                cards += "\t\t\t\t\t\t\t\t\t\t<span class=\"epc-ep-na-card__title\">" + H(name) + "</span>\n";
            }

            if (price > 0)
            {
                cards += "\t\t\t\t\t\t\t\t\t\t<span class=\"epc-ep-na-card__price\">" + PhpNumberFormat(price) + " AED</span>\n";
            }

            cards += "\t\t\t\t\t\t\t\t\t</span>\n\t\t\t</a>\n";
        }

        LastOutput =
            "<section class=\"epc-ep-new-arrivals epc-asp-home-section\" aria-labelledby=\"epc_ep_na_title\">\n" +
            "\t<div class=\"container\">\n" +
            "\t\t<h2 class=\"epc-asp-home-section__title\" id=\"epc_ep_na_title\">New arrivals</h2>\n" +
            "\t\t<p class=\"epc-asp-home-section__lead\">Latest spare parts added to our catalogue — brand / part number URLs, market prices where available.</p>\n" +
            "\t\t<div class=\"epc-ep-na-grid\">\n" +
            cards +
            "\t\t\t\t\t</div>\n" +
            "\t</div>\n" +
            "</section>\n" +
            "<style>\n" +
            ".epc-ep-na-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:14px;margin-top:16px}\n" +
            ".epc-ep-na-card{display:flex;flex-direction:column;background:#fff;border:1px solid #e5eaf2;border-radius:14px;overflow:hidden;text-decoration:none;color:inherit}\n" +
            ".epc-ep-na-card img{width:100%;height:140px;object-fit:contain;background:#f8fafc}\n" +
            ".epc-ep-na-card__body{padding:12px 14px}\n" +
            ".epc-ep-na-card__id{display:block;font-size:13px;color:#dc2626;margin-bottom:4px}\n" +
            ".epc-ep-na-card__title{display:block;font-size:13px;color:#334155;line-height:1.35}\n" +
            ".epc-ep-na-card__price{display:block;font-weight:800;margin-top:6px}\n" +
            "</style>\n";
        return LastOutput;
    }

    public static Dictionary<string, object?> DefaultCard(Dictionary<string, object?> row, string productUrlMode)
    {
        var pid = ToInt(row.ContainsKey("product_id") ? row["product_id"] : row.GetValueOrDefault("id"));
        var catUrl = Str(row.GetValueOrDefault("category_url")).Trim('/');
        var slug = productUrlMode == "id" ? pid.ToString(CultureInfo.InvariantCulture) : Str(row.GetValueOrDefault("alias"));
        var href = catUrl != "" && slug != "" ? "/" + catUrl + "/" + slug : "/shop/products/product?id=" + pid;
        var image = !PhpEmpty(row.GetValueOrDefault("file_name")) ? Str(row.GetValueOrDefault("file_name")) : "";
        var price = ToFloat(row.GetValueOrDefault("price"));
        string displayName;
        if (row.ContainsKey("caption") && row["caption"] is not null)
        {
            displayName = Str(row["caption"]);
        }
        else
        {
            displayName = Str(row.GetValueOrDefault("title"));
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = pid,
            ["brand"] = "",
            ["name"] = displayName,
            ["price"] = price,
            ["was"] = 0,
            ["image"] = image,
            ["href"] = href,
            ["alt"] = displayName,
            ["sku"] = ""
        };
    }

    public static Dictionary<string, string> ParseAlias(string alias)
    {
        var pos = alias.IndexOf('-', StringComparison.Ordinal);
        if (pos < 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "", ["article"] = "" };
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brand"] = alias[..pos],
            ["article"] = alias[(pos + 1)..]
        };
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

    private static string PhpNumberFormat(double value)
        => value.ToString("N2", CultureInfo.GetCultureInfo("en-US"));

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

    private static double ToFloat(object? value)
    {
        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string Coalesce(Dictionary<string, object?> row, string key, string fallback)
        => row.TryGetValue(key, out var value) && value is not null ? Str(value) : fallback;

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
