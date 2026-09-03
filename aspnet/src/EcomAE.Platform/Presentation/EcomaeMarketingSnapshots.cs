using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP-rendered www.ecomae.com marketing pages captured by
/// <c>scripts/render_ecomae_marketing_snapshots.php</c> (the PHP marketing router
/// itself renders each page) — byte-parity for the WHOLE marketing site.
/// Served at the PHP-canonical URLs on marketing hosts; the home stays on the
/// ported Blazor app (/marketing/app).
/// Asset URLs inside snapshots are rewritten to /platform-assets so pages stay
/// styled when product PHP HTTP is paused.
/// </summary>
public static class EcomaeMarketingSnapshots
{
    private const string SnapshotDir = "content/general_pages/epc_rendered_marketing";

    /// <summary>Bare SEO aliases → PHP-canonical snapshot paths.</summary>
    private static readonly Dictionary<string, string> PathAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/about"] = "/platform/about",
        ["/contact"] = "/platform/contact",
        ["/industries"] = "/platform/industries",
        ["/pricing"] = "/platform/pricing",
        ["/demo"] = "/platform/demo",
        ["/faq"] = "/platform/faq",
        ["/capabilities"] = "/platform/capabilities",
        ["/free-tools"] = "/platform/free-tools",
        ["/tools"] = "/platform/free-tools",
        ["/platform/brochure"] = "/brochure",
        ["/platform/brochure/cp"] = "/brochure/cp",
        ["/brochure-cp"] = "/brochure/cp",
        ["/platform/catalog-api"] = "/platform/api-services",
        ["/platform/price-pro-api"] = "/platform/api-services",
        ["/platform/customer-testimonials"] = "/platform/customer-results",
    };

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

        var html = PhpHomeWidgetHtml.RenderStatic(SnapshotDir + "/" + slug + ".html");
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return RewritePhpAssetUrls(html);
    }

    /// <summary>
    /// Home / industry pills deep-link <c>/platform/demo?industry=fashion</c> (or
    /// <c>erp_only</c> / <c>auto_parts</c>). Snapshots are static with auto_parts
    /// checked — apply the query so a new applicant lands on the industry they picked.
    /// Unknown codes (electronics, jewellery, …) stay on the baked default; never invent
    /// extra radios. <c>erp_standalone</c> maps to the ERP-only preset.
    /// </summary>
    public static string ApplyDemoIndustryPref(string html, string? industry)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(industry))
        {
            return html;
        }

        var code = industry.Trim().ToLowerInvariant();
        if (code is "erp_standalone")
        {
            code = "erp_only";
        }

        if (code is not ("auto_parts" or "fashion" or "erp_only"))
        {
            return html;
        }

        html = Regex.Replace(
            html,
            @"<input\b[^>]*\bname=""epm_industry""[^>]*>",
            m => Regex.Replace(
                m.Value,
                @"\s+checked(?:=(?:""[^""]*""|'[^']*'|[^\s>]+))?",
                "",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return Regex.Replace(
            html,
            $@"(<input\b[^>]*\bname=""epm_industry""[^>]*\bvalue=""{Regex.Escape(code)}""[^>]*)(>)",
            "$1 checked$2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

        if (PathAliases.TryGetValue(value, out var aliased))
        {
            value = aliased;
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

    /// <summary>
    /// Snapshots were rendered against PHP HTTP. Product HTML must never emit
    /// active <c>.php</c> asset/page URLs — rewrite to Kestrel <c>/platform-assets</c>
    /// and ASP.NET marketing routes. PHP remains compare-only under <c>/php-reference/*</c>.
    /// </summary>
    public static string RewritePhpAssetUrls(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        // Marketing chrome CSS (primary unstyled-page failure).
        html = Regex.Replace(
            html,
            @"/content/general_pages/epc_ecomae_platform_marketing_css\.php(\?[^""'\s]*)?",
            "/platform-assets/epc_ecomae_platform_marketing.css?v=20260807b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"/epc-static\.php\?f=content/general_pages/epc_ecomae_platform_marketing\.css([^""'\s]*)?",
            "/platform-assets/epc_ecomae_platform_marketing.css?v=20260807b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"/epc-static\.php\?f=content/general_pages/epc_ecomae_home_sections\.css([^""'\s]*)?",
            "/platform-assets/epc_ecomae_home_sections.css?v=20260807b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"/epc-static\.php\?f=content/general_pages/epc_ecomae_home_3d\.css([^""'\s]*)?",
            "/platform-assets/epc_ecomae_home_3d.css?v=20260807b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"/epc-static\.php\?f=content/general_pages/epc_ecomae_home_3d\.js([^""'\s]*)?",
            "/platform-assets/epc_ecomae_home_3d.js?v=20260807b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"/content/general_pages/epc_ecomae_logo_svg\.php",
            "/platform-assets/ecomae-mark.svg",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Screenshots / OG — stack-neutral platform-assets (never product .php).
        html = Regex.Replace(
            html,
            @"/epc-static\.php\?f=content/general_pages/marketing_screens/([^""'\s&]+)(?:&amp;|&)?v?=?([^""'\s]*)?",
            "/platform-assets/marketing_screens/$1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"https?://(?:www\.)?ecomae\.com/epc-static\.php\?f=content/general_pages/marketing_screens/([^""'\s&]+)(?:&amp;|&)?v?=?([^""'\s]*)?",
            "https://www.ecomae.com/platform-assets/marketing_screens/$1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        html = Regex.Replace(
            html,
            @"/(?:content/general_pages/)(marketing_screens/[^""'\s?]+)",
            "/platform-assets/$1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Public verify UI is ASP.NET — never leave the PHP script in product HTML.
        html = Regex.Replace(
            html,
            @"/epc-blockchain-verify\.php",
            "/blockchain/verify",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Catch-all: remaining epc-static.php?f=content/general_pages/X → /platform-assets/X
        html = Regex.Replace(
            html,
            @"/epc-static\.php\?f=content/general_pages/([^""'\s&]+)(?:&amp;|&v=[^""'\s]*)?",
            "/platform-assets/$1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return html;
    }
}
