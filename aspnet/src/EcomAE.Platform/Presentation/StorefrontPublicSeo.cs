using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Public storefront SEO for ASP.NET-primary tenant homes (epartscart, …).
/// Matches PHP nero head signals: indexable home, canonical, Open Graph, hreflang, JSON-LD.
/// CP/ERP/BOS and private storefront apps stay noindex.
/// </summary>
public static class StorefrontPublicSeo
{
    public const string DefaultDescription = "eParts Cart (Autoparts)";
    public const string DefaultKeywords = "eParts Cart (Autoparts)";
    public const string PhpSitemapIndex = "/sitemap-index.php";
    public const string SitemapXmlPath = "/sitemap.xml";

    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string RobotsContentFor(HttpContext? http)
    {
        var path = http?.Request.Path.Value ?? "/";
        return IsPublicIndexablePath(path) ? "index,follow" : "noindex,nofollow,noarchive";
    }

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
        if (value.Equals("/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/storefront", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/storefront/app", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Public marketing surface on Super-CP / ecomae.com.
        if (value.Equals("/marketing", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/marketing/app", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase))
        {
            // Legal/policy pages may still be indexed; admin digests are not under /marketing.
            return true;
        }

        return false;
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
                ["areaServed"] = new object[]
                {
                    new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "United Arab Emirates" },
                    new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Saudi Arabia" },
                    new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Oman" },
                    new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Qatar" },
                    new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Bahrain" },
                    new Dictionary<string, object?> { ["@type"] = "Country", ["name"] = "Kuwait" },
                    new Dictionary<string, object?> { ["@type"] = "Place", ["name"] = "Worldwide" },
                },
            },
        };

        return string.Join(
            "\n",
            payloads.Select(p =>
                "<script type=\"application/ld+json\">"
                + JsonSerializer.Serialize(p, JsonLdOptions)
                + "</script>"));
    }
}
