using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Public storefront SEO for ASP.NET-primary tenant homes + CHPU part pages (epartscart, …).
/// Matches PHP nero / epc_seo_indexing signals: indexable home + in-stock brand/article CHPU,
/// canonical, Open Graph, hreflang, JSON-LD Product schema.
/// CP/ERP/BOS, private storefront apps, and brand-picker hubs stay noindex.
/// </summary>
public static class StorefrontPublicSeo
{
    public const string DefaultDescription = "eParts Cart (Autoparts)";
    public const string DefaultKeywords = "eParts Cart (Autoparts), auto parts UAE, spare parts, OE parts, aftermarket";
    /// <summary>PHP-parity home meta description (warehouse regional shipping phrase).</summary>
    public const string HomeMetaDescription =
        "Buy genuine and aftermarket auto parts online at eParts Cart (Autoparts). UAE-Oman-KSA warehouse — Fast ship to GCC and worldwide.";
    public const string RegionalShippingPhrase =
        "UAE-Oman-KSA warehouse — Fast ship to GCC and worldwide";
    public const string BingSiteVerification = "A5F1A0C564CD9037AAD1E7874D8F4FA8";
    public const string PhpSitemapIndex = "/sitemap-index.php";
    public const string SitemapXmlPath = "/sitemap.xml";

    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private static readonly Regex PartsBrandArticlePath = new(
        @"^/(?:en|ar|ru)/parts/(?!brands(?:/|$))(?<brand>[^/]+)/(?<article>[^/]+)/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PartsBrandArticlePathNoLang = new(
        @"^/parts/(?!brands(?:/|$))(?<brand>[^/]+)/(?<article>[^/]+)/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string RobotsContentFor(HttpContext? http)
    {
        var path = http?.Request.Path.Value ?? "/";
        // CHPU brand+article pages own stock-aware robots in StorefrontSearchApp — avoid dual tags.
        if (PageOwnsRobotsMeta(path))
        {
            return "index,follow";
        }

        return IsPublicIndexablePath(path) ? "index,follow" : "noindex,nofollow,noarchive";
    }

    /// <summary>True when the page component emits stock-aware robots (omit shell robots).</summary>
    public static bool PageOwnsRobotsMeta(string? path)
        => TryParsePartsChpu(path, out _, out _);

    public static bool IsPublicIndexablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var value = path.Trim();
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        value = value.TrimEnd('/');
        if (value.Length == 0)
        {
            value = "/";
        }

        // ASP.NET-primary storefront home (nginx / → /storefront/app).
        // Explicit multilang homes (/en/, /ar/) are the same page — must stay indexable.
        if (value.Equals("/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/storefront", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/storefront/app", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/en", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/ar", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/me", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/ru", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Public marketing surface on Super-CP / ecomae.com.
        if (value.Equals("/marketing", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/marketing/app", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // PHP prices_by_article_and_manufacturer CHPU — indexable warehouse product URLs.
        // Brand-picker hubs (/en/parts/brands/{article}) stay noindex (PHP all_brands_by_article).
        if (TryParsePartsChpu(value, out _, out _))
        {
            return true;
        }

        return false;
    }

    public static bool TryParsePartsChpu(string? path, out string brand, out string article)
    {
        brand = string.Empty;
        article = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var value = path.Trim();
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        value = value.TrimEnd('/');
        var m = PartsBrandArticlePath.Match(value);
        if (!m.Success)
        {
            m = PartsBrandArticlePathNoLang.Match(value);
        }

        if (!m.Success)
        {
            return false;
        }

        brand = Uri.UnescapeDataString(m.Groups["brand"].Value.Replace('+', ' ')).Trim();
        article = Uri.UnescapeDataString(m.Groups["article"].Value.Replace('+', ' ')).Trim();
        if (brand.Length == 0
            || article.Length == 0
            || brand.Equals("brands", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static string AbsoluteUrl(HttpRequest request, string pathAndQuery)
    {
        var path = string.IsNullOrWhiteSpace(pathAndQuery) ? "/" : pathAndQuery.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var host = request.Host.Value;
        var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var proto)
            && !string.IsNullOrWhiteSpace(proto)
            ? proto.ToString().Split(',')[0].Trim()
            : request.Scheme;
        if (string.IsNullOrWhiteSpace(scheme))
        {
            scheme = "https";
        }

        return $"{scheme}://{host}{path}";
    }

    public static string CanonicalForStorefrontHome(HttpRequest request)
        => AbsoluteUrl(request, "/");

    public static string CanonicalForPartsChpu(HttpRequest request, string brand, string article)
    {
        var br = (brand ?? string.Empty).Trim().ToUpperInvariant();
        var art = (article ?? string.Empty).Trim();
        var path = "/en/parts/" + Uri.EscapeDataString(br) + "/" + Uri.EscapeDataString(art);
        return AbsoluteUrl(request, path);
    }

    /// <summary>PHP <c>epc_seo_format_part_title</c>.</summary>
    public static string PartsChpuTitle(string brand, string article, string? siteName = null)
    {
        var br = (brand ?? string.Empty).Trim().ToUpperInvariant();
        var art = (article ?? string.Empty).Trim();
        var site = string.IsNullOrWhiteSpace(siteName) ? DefaultDescription : siteName.Trim();
        return $"{br} {art} — Part number {art} | {site}";
    }

    /// <summary>PHP <c>epc_seo_format_part_description</c>.</summary>
    public static string PartsChpuDescription(string brand, string article, string? productName = null, bool inStock = true)
    {
        var br = (brand ?? string.Empty).Trim().ToUpperInvariant();
        var art = (article ?? string.Empty).Trim();
        var name = string.IsNullOrWhiteSpace(productName) ? string.Empty : productName.Trim();
        var bits = new List<string>
        {
            "Part number / article: " + art,
            "Brand: " + br,
        };
        if (name.Length > 0)
        {
            bits.Add(name);
        }

        if (inStock)
        {
            bits.Add("In stock at UAE warehouse");
        }

        bits.Add(RegionalShippingPhrase);
        return string.Join(". ", bits) + ".";
    }

    /// <summary>PHP warehouse enrichment keywords for brand+article CHPU.</summary>
    public static string PartsChpuKeywords(string brand, string article)
    {
        var br = (brand ?? string.Empty).Trim().ToUpperInvariant();
        var art = (article ?? string.Empty).Trim();
        return string.Join(", ", new[]
        {
            art,
            $"{br} {art}",
            "part number " + art,
            "article " + art,
            "spare parts",
            "auto parts UAE",
        }.Where(static s => !string.IsNullOrWhiteSpace(s)));
    }

    public static string PartsChpuRobots(bool inStock)
        => inStock ? "index,follow" : "noindex,follow";

    public static IReadOnlyList<(string Hreflang, string Href)> HreflangAlternates(HttpRequest request)
    {
        var origin = AbsoluteUrl(request, "/").TrimEnd('/');
        return
        [
            ("en", origin + "/en/"),
            ("ar", origin + "/ar/"),
            ("ru", origin + "/ru/"),
            ("en-AE", origin + "/en/"),
            ("ar-AE", origin + "/ar/"),
            ("en-SA", origin + "/en/"),
            ("en-OM", origin + "/en/"),
            ("en-PK", origin + "/en/"),
            ("x-default", origin + "/en/"),
        ];
    }

    public static IReadOnlyList<(string Hreflang, string Href)> HreflangAlternatesForParts(
        HttpRequest request,
        string brand,
        string article)
    {
        var br = Uri.EscapeDataString((brand ?? string.Empty).Trim().ToUpperInvariant());
        var art = Uri.EscapeDataString((article ?? string.Empty).Trim());
        var origin = AbsoluteUrl(request, "/").TrimEnd('/');
        string PathFor(string lang) => $"{origin}/{lang}/parts/{br}/{art}";
        return
        [
            ("en", PathFor("en")),
            ("ar", PathFor("ar")),
            ("ru", PathFor("ru")),
            ("en-AE", PathFor("en")),
            ("ar-AE", PathFor("ar")),
            ("en-SA", PathFor("en")),
            ("en-OM", PathFor("en")),
            ("en-PK", PathFor("en")),
            ("x-default", PathFor("en")),
        ];
    }

    /// <summary>PHP <c>epc_seo_geo_meta_html</c> + Bing verification for epartscart hosts.</summary>
    public static string HeadExtrasHtml(HttpRequest request)
    {
        var host = request.Host.Host ?? string.Empty;
        var isEparts = host.Contains("epartscart", StringComparison.OrdinalIgnoreCase);
        var sb = new System.Text.StringBuilder();
        if (isEparts)
        {
            sb.Append("<meta name=\"msvalidate.01\" content=\"").Append(BingSiteVerification).Append("\" />\n");
            sb.Append("<meta name=\"geo.region\" content=\"AE-DU\" />\n");
            sb.Append("<meta name=\"geo.placename\" content=\"Dubai, United Arab Emirates\" />\n");
            sb.Append("<meta name=\"geo.position\" content=\"25.2048;55.2708\" />\n");
            sb.Append("<meta name=\"ICBM\" content=\"25.2048, 55.2708\" />\n");
        }

        return sb.ToString();
    }

    private static object[] AreaServedEntries() =>
    [
        new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "United Arab Emirates" },
        new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Saudi Arabia" },
        new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Oman" },
        new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Qatar" },
        new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Bahrain" },
        new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Kuwait" },
        new Dictionary<string, object?> { ["@type"] = "Place", ["name"] = "Worldwide" },
    ];

    public static string JsonLdBlock(HttpRequest request, string storeName)
    {
        var origin = AbsoluteUrl(request, "/").TrimEnd('/');
        var orgName = string.IsNullOrWhiteSpace(storeName) ? DefaultDescription : storeName.Trim();
        var payloads = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Organization",
                ["name"] = "e-world Commerce System",
                ["url"] = origin,
                ["logo"] = origin + "/favicon.svg",
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["addressCountry"] = "United Arab Emirates",
                },
            },
            new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "WebSite",
                ["name"] = "e-world Commerce System",
                ["url"] = origin,
                ["potentialAction"] = new Dictionary<string, object?>
                {
                    ["@type"] = "SearchAction",
                    ["target"] = origin + "/en/shop/search?search_string={search_term_string}",
                    ["query-input"] = "required name=search_term_string",
                },
            },
            new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = new[] { "Organization", "AutoPartsStore" },
                ["name"] = orgName,
                ["url"] = origin,
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["addressLocality"] = "Dubai",
                    ["addressRegion"] = "Dubai",
                    ["addressCountry"] = "AE",
                },
                ["areaServed"] = AreaServedEntries(),
            },
        };

        return string.Join(
            "\n",
            payloads.Select(p =>
                "<script type=\"application/ld+json\">"
                + JsonSerializer.Serialize(p, JsonLdOptions)
                + "</script>"));
    }

    /// <summary>PHP <c>epc_seo_build_product_schema_array</c> subset for CHPU brand+article pages.</summary>
    public static string ProductJsonLdBlock(
        HttpRequest request,
        string brand,
        string article,
        string? productName,
        decimal price,
        bool inStock,
        string currencyCode = "AED",
        IReadOnlyList<(string Brand, string Article)>? crossRefs = null)
    {
        var br = (brand ?? string.Empty).Trim().ToUpperInvariant();
        var art = (article ?? string.Empty).Trim();
        var artNorm = new string(art.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var pageUrl = CanonicalForPartsChpu(request, br, art);
        var name = string.IsNullOrWhiteSpace(productName)
            ? $"{br} {art}"
            : $"{br} {art} {productName.Trim()}";
        var offer = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["url"] = pageUrl,
            ["availability"] = inStock
                ? "https://schema.org/InStock"
                : "https://schema.org/OutOfStock",
            ["itemCondition"] = "https://schema.org/NewCondition",
            ["seller"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = DefaultDescription,
            },
            ["areaServed"] = AreaServedEntries(),
            ["shippingDetails"] = new Dictionary<string, object?>
            {
                ["@type"] = "OfferShippingDetails",
                ["shippingDestination"] = new Dictionary<string, object?>
                {
                    ["@type"] = "DefinedRegion",
                    ["name"] = "GCC and Worldwide",
                },
                ["deliveryTime"] = new Dictionary<string, object?>
                {
                    ["@type"] = "ShippingDeliveryTime",
                    ["handlingTime"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "QuantitativeValue",
                        ["minValue"] = 0,
                        ["maxValue"] = 2,
                        ["unitCode"] = "DAY",
                    },
                },
            },
        };
        if (price > 0m)
        {
            offer["priceCurrency"] = string.IsNullOrWhiteSpace(currencyCode) ? "AED" : currencyCode;
            offer["price"] = price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        var props = new List<object>
        {
            new Dictionary<string, object?> { ["@type"] = "PropertyValue", ["name"] = "Part number", ["value"] = art },
            new Dictionary<string, object?> { ["@type"] = "PropertyValue", ["name"] = "Article number", ["value"] = artNorm },
            new Dictionary<string, object?> { ["@type"] = "PropertyValue", ["name"] = "Brand", ["value"] = br },
        };
        var related = new List<object>();
        foreach (var cr in (crossRefs ?? Array.Empty<(string, string)>()).Take(25))
        {
            var cb = (cr.Brand ?? string.Empty).Trim();
            var ca = (cr.Article ?? string.Empty).Trim();
            if (ca.Length == 0)
            {
                continue;
            }

            props.Add(new Dictionary<string, object?>
            {
                ["@type"] = "PropertyValue",
                ["name"] = "Cross reference / OE",
                ["value"] = string.IsNullOrWhiteSpace(cb) ? ca : $"{cb} {ca}",
            });
            related.Add(new Dictionary<string, object?>
            {
                ["@type"] = "Product",
                ["name"] = string.IsNullOrWhiteSpace(cb) ? ca : $"{cb} {ca}",
                ["sku"] = new string(ca.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()),
                ["brand"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Brand",
                    ["name"] = string.IsNullOrWhiteSpace(cb) ? "OE" : cb,
                },
            });
        }

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = name,
            ["sku"] = artNorm.Length > 0 ? artNorm : art,
            ["mpn"] = artNorm.Length > 0 ? artNorm : art,
            ["productID"] = artNorm.Length > 0 ? artNorm : art,
            ["brand"] = new Dictionary<string, object?> { ["@type"] = "Brand", ["name"] = br },
            ["additionalProperty"] = props,
            ["offers"] = offer,
            ["url"] = pageUrl,
            ["areaServed"] = AreaServedEntries(),
        };
        if (related.Count > 0)
        {
            schema["isRelatedTo"] = related;
        }

        return "<script type=\"application/ld+json\">"
               + JsonSerializer.Serialize(schema, JsonLdOptions)
               + "</script>";
    }
}
