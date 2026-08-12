using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Api.Catalog;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>part_search</c> fitment twin: UMAPI cache (analogs → article_links) then
/// <c>api/epartscross_fitment.js.php</c> crossbase widget fallback — without product <c>.php</c>.
/// </summary>
public interface IStorefrontFitmentService
{
    Task<StorefrontFitmentLookupResult> LookupAsync(
        string article,
        string brand,
        string? language = null,
        CancellationToken cancellationToken = default);

    Task<string> GetWidgetJsAsync(
        string article,
        string? language = null,
        CancellationToken cancellationToken = default);

    Task<string> GetTableHtmlAsync(
        string article,
        string? language = null,
        string? cartype = null,
        Stream? requestBody = null,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontFitmentLookupResult(
    bool Ok,
    string Article,
    string Brand,
    string Source,
    string Message,
    object? PC,
    object? CV,
    object? Motorcycle,
    int? ArticleId,
    bool FallbackWidget);

public sealed class StorefrontFitmentService : IStorefrontFitmentService
{
    private static readonly HttpClient SharedHttp = CreateSharedClient();
    private readonly ICatalogOfflineCacheService _offlineCache;
    private readonly IHttpClientFactory? _httpClientFactory;

    public StorefrontFitmentService(
        ICatalogOfflineCacheService offlineCache,
        IHttpClientFactory? httpClientFactory = null)
    {
        _offlineCache = offlineCache;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<StorefrontFitmentLookupResult> LookupAsync(
        string article,
        string brand,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var art = (article ?? string.Empty).Trim();
        var br = (brand ?? string.Empty).Trim();
        var lang = NormalizeLang(language);
        if (art.Length == 0 || br.Length == 0)
        {
            return new StorefrontFitmentLookupResult(
                false, art, br, "rejected", "Article and brand are required.",
                null, null, null, null, true);
        }

        var analogs = await _offlineCache
            .LookupAnalogsAsync("passenger", art, br, lang, "WWW", cancellationToken)
            .ConfigureAwait(false);
        if (!analogs.Ok || analogs.Payload is null)
        {
            return new StorefrontFitmentLookupResult(
                false, art, br, "cache_miss",
                "No cached analogs; use cross-reference fitment widget.",
                null, null, null, null, true);
        }

        var artId = ExtractArticleId(analogs.Payload, art, br);
        if (artId is null or <= 0)
        {
            return new StorefrontFitmentLookupResult(
                false, art, br, "no_art_id",
                "Article ID not found in saved catalog; use cross-reference fitment widget.",
                null, null, null, null, true);
        }

        var links = await _offlineCache
            .LookupArticleLinksAsync("passenger", artId.Value, lang, "WWW", cancellationToken)
            .ConfigureAwait(false);
        if (!links.Ok || links.Payload is null)
        {
            return new StorefrontFitmentLookupResult(
                false, art, br, "links_miss",
                "No saved vehicle links; use cross-reference fitment widget.",
                null, null, null, artId, true);
        }

        var (pc, cv, moto, total) = ExtractVehicleSections(links.Payload);
        if (total <= 0)
        {
            return new StorefrontFitmentLookupResult(
                false, art, br, "empty_links",
                "No vehicle fitment rows in saved catalog.",
                pc, cv, moto, artId, true);
        }

        return new StorefrontFitmentLookupResult(
            true, art, br, "database", string.Empty, pc, cv, moto, artId, false);
    }

    public async Task<string> GetWidgetJsAsync(
        string article,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var art = (article ?? string.Empty).Trim();
        var lang = NormalizeLang(language);
        if (art.Length == 0)
        {
            return "/* epartscross: missing article */";
        }

        var upstream = "https://crossbase.ru/prim/getjs/index.php?n="
            + Uri.EscapeDataString(art)
            + "&lang=" + Uri.EscapeDataString(lang)
            + "&cartype=UNI";
        var body = await FetchTextAsync(upstream, null, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            return "/* epartscross fitment temporarily unavailable */";
        }

        // Tenant-safe: keep vendor hostnames out of the storefront HTML/JS surface.
        var tableUrl = "/storefront/fitment-table?n=" + Uri.EscapeDataString(art)
            + "&lang=" + Uri.EscapeDataString(lang)
            + "&cartype=UNI";
        body = Regex.Replace(
            body,
            @"https://crossbase\.ru/prim/getjs/gettable\.php\?[^'""\s]+",
            tableUrl,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return body;
    }

    public async Task<string> GetTableHtmlAsync(
        string article,
        string? language = null,
        string? cartype = null,
        Stream? requestBody = null,
        CancellationToken cancellationToken = default)
    {
        var art = (article ?? string.Empty).Trim();
        var lang = NormalizeLang(language);
        var type = string.IsNullOrWhiteSpace(cartype) ? "UNI" : cartype.Trim();
        if (art.Length == 0)
        {
            return "<div class=\"epc-fitment-message\">Missing article for fitment table.</div>";
        }

        var upstream = "https://crossbase.ru/prim/getjs/gettable.php?n="
            + Uri.EscapeDataString(art)
            + "&lang=" + Uri.EscapeDataString(lang)
            + "&cartype=" + Uri.EscapeDataString(type);

        string? postJson = null;
        if (requestBody is not null)
        {
            using var reader = new StreamReader(requestBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            postJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(postJson))
        {
            postJson = "{}";
        }

        var html = await FetchTextAsync(upstream, postJson, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(html))
        {
            return "<div class=\"epc-fitment-message\">Vehicle fitment is temporarily unavailable. Try again later.</div>";
        }

        return html;
    }

    public static int? ExtractArticleId(object payload, string article, string brand)
    {
        if (payload is not JsonElement root)
        {
            return null;
        }

        var rows = EnumerateRows(root);
        JsonElement? preferred = null;
        JsonElement? brandOnly = null;
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var rowBrand = FirstString(row, "BRAND", "SUP_BRAND", "brand", "manufacturer");
            var rowArt = FirstString(row, "ARTICLE_NR", "ART_ARTICLE_NR", "ARTICLE", "DISPLAY_NR", "article");
            if (Compact(rowBrand) == Compact(brand) && Compact(rowArt) == Compact(article))
            {
                preferred = row;
                break;
            }

            if (brandOnly is null && Compact(rowBrand) == Compact(brand))
            {
                brandOnly = row;
            }
        }

        var target = preferred ?? brandOnly ?? (rows.Count > 0 ? rows[0] : null);
        if (target is null)
        {
            return null;
        }

        return FirstInt(target.Value, "ART_ID", "art_id", "id");
    }

    public static (object? PC, object? CV, object? Motorcycle, int Total) ExtractVehicleSections(object payload)
    {
        if (payload is not JsonElement root)
        {
            return (null, null, null, 0);
        }

        var node = root;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && (HasSection(data, "PC") || HasSection(data, "CV") || HasSection(data, "Motorcycle")))
        {
            node = data;
        }

        var pc = SectionArray(node, "PC");
        var cv = SectionArray(node, "CV");
        var moto = SectionArray(node, "Motorcycle");
        var total = Count(pc) + Count(cv) + Count(moto);
        return (pc, cv, moto, total);
    }

    public static string RewriteWidgetJsForTests(string body, string article, string lang)
    {
        var tableUrl = "/storefront/fitment-table?n=" + Uri.EscapeDataString(article)
            + "&lang=" + Uri.EscapeDataString(lang)
            + "&cartype=UNI";
        return Regex.Replace(
            body,
            @"https://crossbase\.ru/prim/getjs/gettable\.php\?[^'""\s]+",
            tableUrl,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static List<JsonElement> EnumerateRows(JsonElement root)
    {
        var list = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                list.Add(el);
            }

            return list;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in data.EnumerateArray())
                {
                    list.Add(el);
                }
            }
        }

        return list;
    }

    private static bool HasSection(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0;

    private static object? SectionArray(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array)
        {
            return el;
        }

        return Array.Empty<object>();
    }

    private static int Count(object? section)
    {
        if (section is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            return el.GetArrayLength();
        }

        if (section is Array arr)
        {
            return arr.Length;
        }

        return 0;
    }

    private static string FirstString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var el))
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    return el.GetString() ?? string.Empty;
                }

                if (el.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return el.ToString();
                }
            }
        }

        return string.Empty;
    }

    private static int? FirstInt(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            {
                return n;
            }

            if (el.ValueKind == JsonValueKind.String
                && int.TryParse(el.GetString(), out var parsed)
                && parsed > 0)
            {
                return parsed;
            }
        }

        return null;
    }

    private static string Compact(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value, "[^A-Za-z0-9]+", string.Empty, RegexOptions.CultureInvariant).ToUpperInvariant();

    private static string NormalizeLang(string? language)
    {
        var lang = (language ?? "en").Trim().ToLowerInvariant();
        return lang == "ru" ? "ru" : "en";
    }

    private async Task<string> FetchTextAsync(string url, string? postJson, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(18));
            using var request = new HttpRequestMessage(
                postJson is null ? HttpMethod.Get : HttpMethod.Post,
                url);
            request.Headers.UserAgent.ParseAdd("ECOM-AE-epartscross-fitment-proxy");
            if (postJson is not null)
            {
                request.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
            }

            var client = ResolveClient();
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
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

    private HttpClient ResolveClient()
    {
        if (_httpClientFactory is not null)
        {
            return _httpClientFactory.CreateClient(nameof(StorefrontFitmentService));
        }

        return SharedHttp;
    }

    private static HttpClient CreateSharedClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }
}
