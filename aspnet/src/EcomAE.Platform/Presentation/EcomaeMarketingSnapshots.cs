namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP-rendered www.ecomae.com marketing pages captured by
/// <c>scripts/render_ecomae_marketing_snapshots.php</c> (the PHP marketing router
/// itself renders each page) — byte-parity for the WHOLE marketing site.
/// Served at the PHP-canonical URLs on marketing hosts; the home stays on the
/// ported Blazor app (/marketing/app).
/// </summary>
public static class EcomaeMarketingSnapshots
{
    private const string SnapshotDir = "content/general_pages/epc_rendered_marketing";

    public static bool IsMarketingHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        return normalized is "www.ecomae.com" or "ecomae.com";
    }

    /// <summary>Canonical marketing path → snapshot HTML; empty when not a snapshot page.</summary>
    public static string HtmlFor(string? path)
    {
        var slug = SlugFor(path);
        if (slug is null)
        {
            return string.Empty;
        }

        return PhpHomeWidgetHtml.RenderStatic(SnapshotDir + "/" + slug + ".html");
    }

    /// <summary>Path → snapshot slug; null when the path is never a marketing snapshot.</summary>
    public static string? SlugFor(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var value = path.Trim();
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        value = "/" + value.Trim('/');
        if (value == "/")
        {
            // Home stays on the ported Blazor marketing app.
            return null;
        }

        // /platform/brochure aliases render the same brochure pages.
        if (value.Equals("/platform/brochure", StringComparison.OrdinalIgnoreCase))
        {
            value = "/brochure";
        }
        else if (value.Equals("/platform/brochure/cp", StringComparison.OrdinalIgnoreCase))
        {
            value = "/brochure/cp";
        }

        // Bare /bos is the product BOS app (Super-CP only) — never a marketing snapshot.
        if (value.Equals("/bos", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = value.Trim('/').ToLowerInvariant().Replace("/", "__");
        if (slug.Length == 0 || slug.Length > 160 || slug.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var ch in slug)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('-' or '_'))
            {
                return null;
            }
        }

        return slug;
    }
}
