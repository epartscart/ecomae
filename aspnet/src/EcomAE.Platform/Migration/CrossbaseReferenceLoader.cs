using System.Net.Http;
using System.Text.RegularExpressions;
using EcomAE.Platform.Api.Catalog;

namespace EcomAE.Platform.Migration;

/// <summary>
/// PHP <c>epc_cross_load_crossbase_references</c> / <c>epc_crossbase_cache</c> twin for ASP.NET.
/// Disk cache first, then short HTTP fetch to crossbase.ru — never blocks CHPU first paint.
/// </summary>
public static class CrossbaseReferenceLoader
{
    private static readonly Regex CrossLinkRegex = new(
        @"<a\s+[^>]*href=[""']/cross/\?q=([^""']+)[""'][^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex TotalRegex = new(
        @"существует.*?([0-9]+).*?замен",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EcomAE-Crossbase/1.0 (+https://www.epartscart.com)");
        return client;
    }

    public static async Task<(IReadOnlyList<StorefrontCrossRefDigest> Refs, int ReportedTotal)> LoadAsync(
        string article,
        int maxRefs,
        CancellationToken cancellationToken)
    {
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized) || maxRefs <= 0)
        {
            return ([], 0);
        }

        var html = ReadDiskCache(normalized);
        if (string.IsNullOrWhiteSpace(html))
        {
            html = await FetchRemoteHtmlAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(html) && html.Length > 400)
            {
                TryWriteDiskCache(normalized, html);
            }
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            return ([], 0);
        }

        var reported = 0;
        var totalMatch = TotalRegex.Match(html);
        if (totalMatch.Success
            && int.TryParse(totalMatch.Groups[1].Value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var n))
        {
            reported = n;
        }

        var refs = ParseHtml(html, normalized, maxRefs);
        if (reported <= 0)
        {
            reported = refs.Count;
        }

        return (refs, reported);
    }

    public static List<StorefrontCrossRefDigest> ParseHtml(string html, string selfNorm, int maxRefs)
    {
        var rows = new List<StorefrontCrossRefDigest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CrossLinkRegex.Matches(html))
        {
            if (rows.Count >= maxRefs)
            {
                break;
            }

            var number = Uri.UnescapeDataString(match.Groups[1].Value.Trim());
            var numberNorm = PriceLookupRequest.NormalizeArticle(number);
            if (string.IsNullOrWhiteSpace(numberNorm) || numberNorm == selfNorm)
            {
                continue;
            }

            var brand = GuessBrand(number, match.Groups[2].Value);
            var key = brand.ToUpperInvariant() + "|" + numberNorm;
            if (!seen.Add(key))
            {
                continue;
            }

            rows.Add(new StorefrontCrossRefDigest(brand, number.Trim(), false, "crossbase"));
        }

        return rows;
    }

    private static string GuessBrand(string number, string linkHtml)
    {
        var text = Regex.Replace(linkHtml ?? string.Empty, "<.*?>", " ", RegexOptions.Singleline);
        text = System.Net.WebUtility.HtmlDecode(text).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "CROSSBASE";
        }

        // Prefer a leading token that is not the article itself.
        var tokens = text.Split([' ', '\t', '\r', '\n', '/', '|', ','], StringSplitOptions.RemoveEmptyEntries);
        var artCompact = Compact(number);
        foreach (var token in tokens)
        {
            var t = token.Trim().Trim(',', ';', '.', ':');
            if (t.Length < 2 || t.Length > 32)
            {
                continue;
            }

            if (Compact(t) == artCompact)
            {
                continue;
            }

            if (t.Any(char.IsLetter))
            {
                return t.ToUpperInvariant();
            }
        }

        return "CROSSBASE";
    }

    private static string Compact(string s)
        => new string((s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static IEnumerable<string> CacheDirCandidates()
    {
        var env = Environment.GetEnvironmentVariable("ECOMAE_CROSSBASE_CACHE_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            yield return env.Trim();
        }

        var cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "content", "shop", "docpart", "cache", "crossbase");
        yield return Path.Combine(cwd, "..", "content", "shop", "docpart", "cache", "crossbase");
        yield return Path.Combine(cwd, "..", "..", "content", "shop", "docpart", "cache", "crossbase");
        yield return "/var/www/epartscart_com/htdocs/content/shop/docpart/cache/crossbase";
        yield return "/home/epartscart/htdocs/www.epartscart.com/content/shop/docpart/cache/crossbase";
    }

    private static string? ResolveCachePath(string normalized)
    {
        foreach (var dir in CacheDirCandidates())
        {
            try
            {
                var full = Path.GetFullPath(dir);
                if (Directory.Exists(full))
                {
                    return Path.Combine(full, normalized + ".html");
                }
            }
            catch
            {
                // ignore bad paths
            }
        }

        // Prefer first writable candidate even if missing (for write).
        foreach (var dir in CacheDirCandidates())
        {
            try
            {
                var full = Path.GetFullPath(dir);
                return Path.Combine(full, normalized + ".html");
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static string ReadDiskCache(string normalized)
    {
        try
        {
            var path = ResolveCachePath(normalized);
            if (path is null || !File.Exists(path))
            {
                return string.Empty;
            }

            var html = File.ReadAllText(path);
            return html.Length > 400 ? html : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryWriteDiskCache(string normalized, string html)
    {
        try
        {
            var path = ResolveCachePath(normalized);
            if (path is null)
            {
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, html);
        }
        catch
        {
            // best-effort cache
        }
    }

    private static async Task<string> FetchRemoteHtmlAsync(string normalized, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(2500));
            var url = "https://crossbase.ru/cross/?q=" + Uri.EscapeDataString(normalized);
            using var response = await Http.GetAsync(url, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }
}
