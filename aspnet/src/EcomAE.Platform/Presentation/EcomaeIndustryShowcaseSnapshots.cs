using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP-rendered industry showcase pages (hubs + sub-industry sites) captured by
/// <c>scripts/render_ecomae_industry_snapshots.php</c>. Served on
/// <c>{slug}.ecomae.com</c> so ASP.NET-primary matches PHP reference look.
/// </summary>
public static class EcomaeIndustryShowcaseSnapshots
{
    private const string SnapshotDir = "content/general_pages/epc_rendered_industry";

    private static readonly HashSet<string> ReservedFirstSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "cp", "erp", "bos", "ip", "api", "lifeos", "storefront", "marketing", "platform",
        "documentation", "php-reference", "platform-assets", "aspnet-php-assets",
        "_framework", "_blazor", "auth", "migration", "en", "me", "ru", "parts", "shop",
        "content", "assets", "favicon.ico", "robots.txt", "sitemap.xml"
    };

    public static bool TryResolveHostSlug(string? host, out string slug)
    {
        slug = string.Empty;
        var normalized = NormalizeHost(host);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        if (!normalized.EndsWith(".ecomae.com", StringComparison.Ordinal)
            || normalized is "www.ecomae.com" or "ecomae.com" or "cp.ecomae.com" or "lifeos.ecomae.com"
            || normalized is "industries.ecomae.com")
        {
            return false;
        }

        var candidate = normalized[..^".ecomae.com".Length];
        if (string.IsNullOrWhiteSpace(candidate)
            || !EcomaeIndustryShowcaseHosts.Slugs.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        slug = candidate;
        return true;
    }

    /// <summary>
    /// <c>industries.ecomae.com</c> (and www) is the industries directory, not one of the
    /// 28 <c>{slug}.ecomae.com</c> hubs. It serves the same page as www
    /// <c>/platform/industries</c>.
    /// </summary>
    public static bool IsIndustriesDirectoryHost(string? host)
    {
        var normalized = NormalizeHost(host);
        return normalized is "industries.ecomae.com";
    }

    /// <summary>Home and the public industries aliases on the directory host.</summary>
    public static bool IsIndustriesDirectoryPath(string? path)
    {
        var value = StripQuery(path);
        return value is "/"
            or "/industries"
            or "/platform/industries"
            or "/index.php"
            or "/storefront/app"
            or "/storefront"
            or "/marketing/app";
    }

    public static string HtmlFor(string? host, string? path)
    {
        if (IsIndustriesDirectoryHost(host) && IsIndustriesDirectoryPath(path))
        {
            return EcomaeMarketingSnapshots.HtmlFor("/platform/industries");
        }

        if (!TryResolveHostSlug(host, out var hostSlug))
        {
            return string.Empty;
        }

        var fileSlug = FileSlugFor(hostSlug, path);
        if (fileSlug is null)
        {
            return string.Empty;
        }

        var html = PhpHomeWidgetHtml.RenderStatic(SnapshotDir + "/" + fileSlug + ".html");
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return InjectAspNetPrimaryMarker(EcomaeMarketingSnapshots.RewritePhpAssetUrls(html));
    }

    /// <summary>
    /// Classic-entry prove looks for <c>ecomae-chrome-surface</c>. Industry HTML is
    /// PHP-look snapshots served by ASP.NET — stamp the platform marker without
    /// Blazor <c>_framework</c> (industry frontend gate forbids those).
    /// </summary>
    internal static string InjectAspNetPrimaryMarker(string html)
    {
        const string marker = "<meta name=\"ecomae-chrome-surface\" content=\"industry-showcase\" />";
        if (string.IsNullOrEmpty(html)
            || html.Contains("ecomae-chrome-surface", StringComparison.Ordinal))
        {
            return html;
        }

        var headClose = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headClose >= 0)
        {
            return html.Insert(headClose, marker);
        }

        return marker + html;
    }

    private static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        var colon = normalized.IndexOf(':');
        if (colon > 0)
        {
            normalized = normalized[..colon];
        }

        if (normalized.StartsWith("www.", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private static string StripQuery(string? path)
    {
        var value = (path ?? "/").Trim();
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        value = "/" + value.Trim('/');
        return value.Length == 0 ? "/" : value;
    }

    public static string? FileSlugFor(string hostSlug, string? path)
    {
        var value = StripQuery(path);
        if (value is "/" or "/storefront/app" or "/storefront" or "/index.php" or "/marketing/app")
        {
            return hostSlug;
        }

        var seg = value.Trim('/').Split('/', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        seg = Regex.Replace(seg, @"[^a-z0-9-]", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(seg) || ReservedFirstSegments.Contains(seg))
        {
            return null;
        }

        return hostSlug + "__" + seg;
    }
}
