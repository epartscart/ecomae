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
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        var colon = normalized.IndexOf(':');
        if (colon > 0)
        {
            normalized = normalized[..colon];
        }

        if (!normalized.EndsWith(".ecomae.com", StringComparison.Ordinal)
            || normalized is "www.ecomae.com" or "ecomae.com" or "cp.ecomae.com" or "lifeos.ecomae.com")
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
    /// Snapshot HTML for an industry host + path. Empty when not an industry showcase page.
    /// Treats nginx remaps of home → <c>/storefront/app</c> as the hub.
    /// </summary>
    public static string HtmlFor(string? host, string? path)
    {
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

        return EcomaeMarketingSnapshots.RewritePhpAssetUrls(html);
    }

    public static string? FileSlugFor(string hostSlug, string? path)
    {
        var value = (path ?? "/").Trim();
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        value = "/" + value.Trim('/');
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
