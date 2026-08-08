using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Renders the PHP front-page catalog widgets (content/product_family_catalog.php,
/// content/umapi_catalog.php, content/available_brands.php, content/vehicle_catalog.php)
/// as exact same-to-same HTML for the ASP.NET storefront home.
///
/// The widget files are static HTML + vanilla JS (fetching /api/umapi_proxy.php and
/// /content/shop/docpart/ajax_epc_product_family.php, both still served live) with only a
/// handful of deterministic PHP echo placeholders — substituted here with the same
/// defaults PHP uses for epartscart (guest visitor, chpu off, /en lang href).
/// </summary>
public static class PhpHomeWidgetHtml
{
    public const string DefaultLangHref = "/en";

    // Same bytes PHP emits from epc_storefront_prices_styles() at the top of available_brands.php.
    private const string PricesStyles =
        "<style>"
        + ".epc-price-login-cta{display:inline-block;font-size:12px;line-height:1.35;color:#64748b}"
        + ".epc-price-login-cta a{font-weight:600;color:#2b78d6;text-decoration:none}"
        + ".epc-price-login-cta a:hover{text-decoration:underline}"
        + ".epc-price-login-cta__sep{color:#94a3b8}"
        + ".epc-price-login-cta__hint{color:#64748b}"
        + ".td_price .epc-price-login-cta{max-width:140px}"
        + ".epc-commerce-login-cta{display:flex;flex-direction:column;align-items:flex-start;gap:6px;max-width:180px}"
        + ".epc-commerce-login-cta .btn{margin:0}"
        + ".epc-commerce-login-cta__sep{font-size:12px;color:#94a3b8}"
        + ".epc-commerce-login-cta__hint{font-size:11px;line-height:1.35;color:#64748b}"
        + "</style>";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DateTime StampUtc, string Html)> Cache = new(StringComparer.Ordinal);
    private static string? _repoRoot;

    public static string ProductFamily() => Render("content/product_family_catalog.php");

    public static string UmapiCatalog() => Render("content/umapi_catalog.php");

    public static string AvailableBrands(bool pricesVisible = false)
        => Render("content/available_brands.php", pricesVisible: pricesVisible);

    public static string VehicleCatalog() => Render("content/vehicle_catalog.php");

    /// <summary>Read a pre-rendered HTML file from the monorepo (no PHP substitution).</summary>
    public static string RenderStatic(string relativePath)
    {
        var root = RepoRoot();
        if (root is null)
        {
            return string.Empty;
        }

        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>Empty string when the widget source is unavailable — caller shows the PHP fallback alert.</summary>
    public static string Render(string relativePath, bool pricesVisible = false)
    {
        var root = RepoRoot();
        if (root is null)
        {
            return string.Empty;
        }

        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return string.Empty;
        }

        var cacheKey = relativePath + (pricesVisible ? "|pv1" : "|pv0");
        var stamp = File.GetLastWriteTimeUtc(path);
        lock (Gate)
        {
            if (Cache.TryGetValue(cacheKey, out var hit) && hit.StampUtc == stamp)
            {
                return hit.Html;
            }
        }

        string html;
        try
        {
            html = Substitute(File.ReadAllText(path), DefaultLangHref, pricesVisible);
        }
        catch (IOException)
        {
            return string.Empty;
        }

        lock (Gate)
        {
            Cache[cacheKey] = (stamp, html);
        }

        return html;
    }

    /// <summary>Exposed for tests.</summary>
    public static string Substitute(string phpSource, string langHref, bool pricesVisible = false)
    {
        var text = phpSource;

        // Known echo placeholders — same values PHP resolves for an epartscart guest.
        text = Replace(text, @"<\?php\s+echo\s+htmlspecialchars\(\$lang_href,\s*ENT_QUOTES,\s*'UTF-8'\);\s*\?>", langHref);
        text = Replace(text, @"<\?php\s+echo\s+rawurlencode\(\$epc_pf_theme_ver\);\s*\?>", "20260718pfCat1");
        text = Replace(text, @"<\?php\s+echo\s+\$epc_(?:pf|umapi|vc|brands)_chpu_on\s*\?\s*'true'\s*:\s*'false';\s*\?>", "false");
        text = Replace(text, @"<\?php\s+echo\s+json_encode\(\$epc_(?:pf|umapi|vc)_chpu_parts_url[^?]*\?>", "\"parts\"");
        text = Replace(text, @"<\?php\s+echo\s+json_encode\(\$epc_(?:pf|umapi|vc)_chpu_brands_url[^?]*\?>", "\"brands\"");
        text = Replace(text, @"<\?php\s+echo\s+json_encode\(\$epc_(?:pf|umapi|vc)_chpu_slash_code[^?]*\?>", "\"%2F\"");

        // Price visibility gate (PHP epc_storefront_prices_helpers — guest/pending hide).
        text = Replace(
            text,
            @"<\?php\s+echo\s+\$epc_brands_prices_visible\s*\?\s*'1'\s*:\s*'0';\s*\?>",
            pricesVisible ? "1" : "0");
        var loginCta = GuestLoginCtaJson(langHref);
        text = Replace(text, @"<\?php\s+echo\s+json_encode\(\$epc_brands_login_cta[^?]*\?>", loginCta);

        // available_brands.php: the opening PHP block also echoes epc_storefront_prices_styles().
        var emitsPriceStyles = text.Contains("epc_storefront_prices_styles()", StringComparison.Ordinal);

        // Strip any remaining PHP blocks (opening config block, guards).
        text = Regex.Replace(text, @"<\?php.*?\?>", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"<\?=.*?\?>", string.Empty, RegexOptions.Singleline);

        if (emitsPriceStyles)
        {
            text = PricesStyles + "\n" + text;
        }

        return text.Trim();
    }

    private static string GuestLoginCtaJson(string langHref)
    {
        // PreferAspNet: point at live ASP.NET login/register (PHP /en/users/* 404 when paused).
        var login = StorefrontSurfaceLinks.Login;
        var signup = StorefrontSurfaceLinks.Registration;
        _ = langHref;
        var html = "<span class=\"epc-price-login-cta\">"
                   + "<a href=\"" + login + "\">Log in</a>"
                   + "<span class=\"epc-price-login-cta__sep\"> or </span>"
                   + "<a href=\"" + signup + "\">register</a>"
                   + "<span class=\"epc-price-login-cta__hint\"> to see prices</span>"
                   + "</span>";
        return System.Text.Json.JsonSerializer.Serialize(html);
    }

    private static string Replace(string input, string pattern, string replacement) =>
        Regex.Replace(input, pattern, replacement.Replace("$", "$$", StringComparison.Ordinal), RegexOptions.Singleline);

    private static string? RepoRoot()
    {
        if (_repoRoot is not null)
        {
            return _repoRoot;
        }

        // Live publishes run from /var/www/ecomae-aspnet/releases/<ts>/platform where
        // walking up never finds the monorepo — honour the env override and the
        // CloudPanel checkout locations before giving up.
        var candidates = new List<string>();
        var envRoot = Environment.GetEnvironmentVariable("ECOMAE_PHP_SOURCE_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            candidates.Add(envRoot);
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                candidates.Add(dir.FullName);
                dir = dir.Parent!;
            }
        }

        candidates.Add("/opt/ecomae-aspnet-source");
        candidates.Add("/root/ecomae");

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "content", "product_family_catalog.php")))
            {
                _repoRoot = candidate;
                return _repoRoot;
            }
        }

        return null;
    }
}
