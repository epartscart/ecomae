using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP sitemap scripts that are public files and were 404 on ASP.NET:
/// <c>sitemap-industries.php</c>, <c>sitemap-marketing.php</c>, <c>sitemap-index.php</c>,
/// <c>sitemap-pages.php</c>, and <c>sitemap-products.php</c>.
/// Industry locs come from the rendered showcase snapshots (no <c>config.php</c>).
/// A missing price or CMS table stays a valid urlset.
/// </summary>
public static class PublicSeoSitemaps
{
    public readonly record struct SitemapUrl(string Loc, string ChangeFreq, string Priority);

    private static readonly string[] LegacyPromoSlugs = ["akciya", "aktsiya", "akcia", "promotion", "promotions"];

    private static readonly (string Path, string Freq, string Priority)[] MarketingStaticPaths =
    [
        ("/", "daily", "1.0"),
        ("/platform", "weekly", "0.9"),
        ("/platform/industries", "weekly", "0.9"),
        ("/platform/capabilities", "monthly", "0.8"),
        ("/platform/auto-price-ai", "monthly", "0.8"),
        ("/platform/pricing", "monthly", "0.8"),
        ("/platform/demo", "monthly", "0.8"),
        ("/platform/faq", "monthly", "0.8"),
        ("/platform/contact", "monthly", "0.8"),
        ("/platform/about", "monthly", "0.7"),
        ("/platform/customer-results", "monthly", "0.7"),
        ("/platform/platform-guides", "monthly", "0.7"),
        ("/platform/business-continuity", "monthly", "0.6"),
        ("/platform/api-documentation", "monthly", "0.7"),
        ("/platform/api-services", "monthly", "0.7"),
        ("/documentation", "weekly", "0.7"),
        ("/compare", "weekly", "0.7"),
        ("/bos", "weekly", "0.7"),
        ("/solutions", "weekly", "0.7"),
        ("/legal", "monthly", "0.6"),
        ("/privacy", "monthly", "0.5"),
        ("/terms", "monthly", "0.5"),
        ("/blockchain", "weekly", "0.9"),
    ];

    private static readonly Lazy<IReadOnlyList<SitemapUrl>> IndustryCache = new(LoadIndustryEntries);
    private static readonly Lazy<IReadOnlyList<string>> MarketingPathCache = new(LoadMarketingSnapshotPaths);

    public const string MarketingOrigin = "https://www.ecomae.com";

    /// <summary>
    /// PHP <c>index.php</c> 301s leftover promo slugs on the marketing host.
    /// Only those slugs (optional /en|/ru|/ar). Other <c>/en/*</c> storefront paths stay.
    /// </summary>
    public static bool IsLegacyPromoRedirect(string? host, string? path)
    {
        if (!EcomaeMarketingSnapshots.IsMarketingHost(host))
        {
            return false;
        }

        var parts = PublicAnonymousPages.NormalizePath(path)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var index = 0;
        if (parts[0].Equals("en", StringComparison.OrdinalIgnoreCase)
            || parts[0].Equals("ru", StringComparison.OrdinalIgnoreCase)
            || parts[0].Equals("ar", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2)
            {
                return false;
            }

            index = 1;
        }

        return LegacyPromoSlugs.Contains(parts[index], StringComparer.OrdinalIgnoreCase);
    }

    public static bool ShouldRedirectIndustriesSitemap(string? host)
        => EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out _);

    /// <summary>PHP <c>sitemap-index.php</c> child list. Warehouse shards stay out until those routes exist.</summary>
    public static IReadOnlyList<string> IndexChildren(string? host)
    {
        if (ShouldRedirectIndustriesSitemap(host))
        {
            return ["sitemap.xml"];
        }

        if (StorefrontPublicSeo.IsEcomaeMarketingHost(host))
        {
            return ["sitemap-industries.php", "sitemap-marketing.php", "sitemap.xml", "sitemap-pages.php"];
        }

        return ["sitemap-pages.php", "sitemap-products.php"];
    }

    public static string SitemapIndex(string origin, IEnumerable<string> children)
    {
        var root = (origin ?? string.Empty).TrimEnd('/');
        var today = Today();
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var child in children)
        {
            if (string.IsNullOrWhiteSpace(child))
            {
                continue;
            }

            var loc = WebUtility.HtmlEncode(root + "/" + child.TrimStart('/'));
            sb.Append("\t<sitemap>\n");
            sb.Append("\t\t<loc>").Append(loc).Append("</loc>\n");
            sb.Append("\t\t<lastmod>").Append(today).Append("</lastmod>\n");
            sb.Append("\t</sitemap>\n");
        }

        sb.Append("</sitemapindex>\n");
        return sb.ToString();
    }

    public static IReadOnlyList<SitemapUrl> IndustryEntries() => IndustryCache.Value;

    public static string IndustryUrlset() => Emit(IndustryEntries());

    public static string IndustryHostUrlset(string? host)
    {
        if (!EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out var slug))
        {
            return Emit([]);
        }

        var prefix = "https://" + slug.ToLowerInvariant() + ".ecomae.com";
        return Emit(IndustryEntries().Where(entry =>
            entry.Loc.Equals(prefix + "/", StringComparison.OrdinalIgnoreCase)
            || entry.Loc.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)));
    }

    public static string MarketingUrlset()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<SitemapUrl>();
        foreach (var (path, freq, priority) in MarketingStaticPaths)
        {
            Add(entries, seen, MarketingOrigin + path, freq, priority);
        }

        foreach (var path in MarketingPathCache.Value)
        {
            Add(entries, seen, MarketingOrigin + path, "monthly", "0.6");
        }

        foreach (var industry in IndustryEntries())
        {
            Add(entries, seen, industry.Loc, industry.ChangeFreq, industry.Priority);
        }

        return Emit(entries);
    }

    public static string MarketingPagesUrlset()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<SitemapUrl>();
        Add(entries, seen, MarketingOrigin + "/", "daily", "1.0");
        foreach (var path in MarketingPathCache.Value)
        {
            Add(entries, seen, MarketingOrigin + path, "weekly", "0.6");
        }

        return Emit(entries);
    }

    public static string ProductHubUrlset(string origin, IEnumerable<string> manufacturers)
    {
        var root = (origin ?? string.Empty).TrimEnd('/');
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<SitemapUrl>();
        Add(entries, seen, root + "/en/parts", "daily", "1.0");
        Add(entries, seen, root + "/en/available-brands", "weekly", "0.8");
        foreach (var manufacturer in manufacturers)
        {
            var segment = BrandSegment(manufacturer);
            if (segment.Length == 0)
            {
                continue;
            }

            Add(entries, seen, root + "/en/parts/" + segment, "weekly", "0.7");
        }

        return Emit(entries);
    }

    public static string ProductBrandUrlset(string origin, string brand, IEnumerable<string> articles)
    {
        var root = (origin ?? string.Empty).TrimEnd('/');
        var brandSegment = BrandSegment(brand);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<SitemapUrl>();
        if (brandSegment.Length == 0)
        {
            return Emit(entries);
        }

        foreach (var article in articles)
        {
            var articleSegment = ArticleSegment(article);
            if (articleSegment.Length == 0)
            {
                continue;
            }

            Add(entries, seen, root + "/en/parts/" + brandSegment + "/" + articleSegment, "weekly", "0.6");
        }

        return Emit(entries);
    }

    public static string TenantPageFallbackUrlset(string origin)
    {
        var root = (origin ?? string.Empty).TrimEnd('/');
        return Emit(
        [
            new SitemapUrl(root + "/en", "daily", "1.0"),
            new SitemapUrl(root + "/en/parts", "daily", "0.9"),
            new SitemapUrl(root + "/en/available-brands", "weekly", "0.8"),
            new SitemapUrl(root + "/en/accessories", "weekly", "0.6"),
            new SitemapUrl(root + "/privacy", "monthly", "0.5"),
            new SitemapUrl(root + "/en/privacy", "monthly", "0.5"),
        ]);
    }

    public static async Task<string> TenantPagesUrlsetAsync(DbConnection connection, string origin, CancellationToken cancellationToken)
    {
        var root = (origin ?? string.Empty).TrimEnd('/');
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<SitemapUrl>();
        await ReadContentPagesAsync(connection, root, entries, seen, cancellationToken).ConfigureAwait(false);
        await ReadCataloguePagesAsync(connection, root, entries, seen, cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return TenantPageFallbackUrlset(root);
        }

        return Emit(entries);
    }

    public static async Task<IReadOnlyList<string>> ReadInStockManufacturersAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var list = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT TRIM(d.`manufacturer`) AS manufacturer
            FROM `shop_docpart_prices_data` d
            WHERE TRIM(IFNULL(d.`manufacturer`, '')) != ''
              AND IFNULL(d.`exist`, 0) > 0
            ORDER BY manufacturer ASC
            LIMIT 5000
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var manufacturer = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
            if (manufacturer.Length > 0)
            {
                list.Add(manufacturer);
            }
        }

        return list;
    }

    public static async Task<IReadOnlyList<string>> ReadBrandArticlesAsync(
        DbConnection connection,
        string brand,
        CancellationToken cancellationToken)
    {
        var list = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT TRIM(d.`article`) AS article
            FROM `shop_docpart_prices_data` d
            WHERE TRIM(d.`manufacturer`) = @brand
              AND TRIM(IFNULL(d.`article`, '')) != ''
              AND IFNULL(d.`exist`, 0) > 0
            ORDER BY article ASC
            LIMIT 45000
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@brand";
        parameter.Value = brand.Trim();
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var article = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
            if (article.Length > 0)
            {
                list.Add(article);
            }
        }

        return list;
    }

    private static async Task ReadContentPagesAsync(
        DbConnection connection,
        string origin,
        List<SitemapUrl> entries,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `url`, `main_flag`
                FROM `content`
                WHERE `is_frontend` = 1
                  AND `published_flag` = 1
                  AND (`robots_tag` IS NULL OR `robots_tag` = '' OR `robots_tag` NOT LIKE '%noindex%')
                  AND `url` NOT IN ('users', 'shop', 'cp')
                  AND `url` NOT LIKE 'shop/part_search%'
                ORDER BY `main_flag` DESC, `url` ASC
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var url = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
                if (url.Length == 0)
                {
                    continue;
                }

                var main = !reader.IsDBNull(1) && Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) == 1;
                Add(entries, seen, LangUrl(origin, url), main ? "daily" : "monthly", main ? "1.0" : "0.5");
            }
        }
        catch (DbException)
        {
        }
    }

    private static async Task ReadCataloguePagesAsync(
        DbConnection connection,
        string origin,
        List<SitemapUrl> entries,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `url`
                FROM `shop_catalogue_categories`
                WHERE `published_flag` = 1
                  AND TRIM(IFNULL(`url`, '')) != ''
                ORDER BY `level` ASC, `order` ASC
                LIMIT 5000
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var url = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
                if (url.Length > 0)
                {
                    Add(entries, seen, LangUrl(origin, url), "weekly", "0.6");
                }
            }
        }
        catch (DbException)
        {
        }
    }

    private static string LangUrl(string origin, string url)
    {
        var path = url.Trim().Trim('/');
        if (path.Length == 0)
        {
            return origin + "/en";
        }

        if (path.Equals("en", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("en/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("ru/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("ar/", StringComparison.OrdinalIgnoreCase))
        {
            return origin + "/" + path;
        }

        return origin + "/en/" + path;
    }

    private static string BrandSegment(string manufacturer)
    {
        var upper = manufacturer.Trim().ToUpperInvariant().Replace("/", "&ms;", StringComparison.Ordinal);
        return upper.Length == 0 ? "" : Uri.EscapeDataString(upper);
    }

    private static string ArticleSegment(string article)
    {
        var sweep = new[] { " ", "-", "_", "`", "/", "'", "\"", "\\", ".", ",", "#", "\r", "\n", "\t" };
        var value = article;
        foreach (var token in sweep)
        {
            value = value.Replace(token, "", StringComparison.Ordinal);
        }

        value = value.Trim().ToUpperInvariant();
        return value.Length == 0 ? "" : Uri.EscapeDataString(value);
    }

    private static void Add(List<SitemapUrl> entries, HashSet<string> seen, string loc, string freq, string priority)
    {
        if (string.IsNullOrWhiteSpace(loc) || !seen.Add(loc))
        {
            return;
        }

        entries.Add(new SitemapUrl(loc, freq, priority));
    }

    private static string Emit(IEnumerable<SitemapUrl> entries)
    {
        var today = Today();
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Loc))
            {
                continue;
            }

            sb.Append("\t<url>\n");
            sb.Append("\t\t<loc>").Append(WebUtility.HtmlEncode(entry.Loc)).Append("</loc>\n");
            sb.Append("\t\t<lastmod>").Append(today).Append("</lastmod>\n");
            sb.Append("\t\t<changefreq>").Append(entry.ChangeFreq).Append("</changefreq>\n");
            sb.Append("\t\t<priority>").Append(entry.Priority).Append("</priority>\n");
            sb.Append("\t</url>\n");
        }

        sb.Append("</urlset>\n");
        return sb.ToString();
    }

    private static string Today()
        => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<SitemapUrl> LoadIndustryEntries()
    {
        var root = RepoRoot();
        if (root is null)
        {
            return [];
        }

        var dir = Path.Combine(root, "content", "general_pages", "epc_rendered_industry");
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var slugs = new HashSet<string>(EcomaeIndustryShowcaseHosts.Slugs, StringComparer.OrdinalIgnoreCase);
        var entries = new List<SitemapUrl>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(dir, "*.html"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var split = stem.IndexOf("__", StringComparison.Ordinal);
            var host = split < 0 ? stem : stem[..split];
            if (!slugs.Contains(host))
            {
                continue;
            }

            var loc = split < 0
                ? "https://" + host.ToLowerInvariant() + ".ecomae.com/"
                : "https://" + host.ToLowerInvariant() + ".ecomae.com/" + stem[(split + 2)..];
            if (split < 0)
            {
                Add(entries, seen, loc, "weekly", "0.85");
            }
            else if (!stem[(split + 2)..].Contains("__", StringComparison.Ordinal))
            {
                Add(entries, seen, loc, "monthly", "0.7");
            }
        }

        return entries;
    }

    private static IReadOnlyList<string> LoadMarketingSnapshotPaths()
    {
        var root = RepoRoot();
        if (root is null)
        {
            return [];
        }

        var dir = Path.Combine(root, "content", "general_pages", "epc_rendered_marketing");
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.html"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.Length == 0 || stem.Contains("..", StringComparison.Ordinal))
            {
                continue;
            }

            var path = "/" + stem.Replace("__", "/", StringComparison.Ordinal);
            if (path.Contains("//", StringComparison.Ordinal))
            {
                continue;
            }

            paths.Add(path);
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static string? RepoRoot()
    {
        var starts = new List<string>();
        var env = Environment.GetEnvironmentVariable("ECOMAE_PHP_SOURCE_ROOT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            starts.Add(env);
        }

        starts.Add(AppContext.BaseDirectory);
        starts.Add(Directory.GetCurrentDirectory());
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "content", "general_pages", "epc_rendered_industry")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}
