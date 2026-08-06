using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Loopback/public bridge to PHP CHPU warehouse endpoints that already use the shop DB via config.php.
/// Used when ASP.NET tenant SQL returns empty (wrong host binding / over-filters on older binaries).
/// Prefers 127.0.0.1 + Host header so nginx still routes to PHP ajax while PHP serving is
/// temporarily deactivated for product HTML (avoids Cloudflare hop / self-proxy failures).
/// Also proxies progressive <c>ajax_getProductsOfBunch</c> supplier polls (session cookies forwarded).
/// </summary>
public sealed class PhpWarehouseSearchBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public PhpWarehouseSearchBridge(
        IHttpClientFactory? httpClientFactory = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<StorefrontPartOfferDigest>> TryLoadOffersAsync(
        string article,
        string? brand,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["article"] = article,
            ["brand"] = brand,
            ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
        };

        var payload = await GetJsonAsync<PhpWarehouseOffersPayload>(
                "/content/shop/docpart/ajax_epc_warehouse_offers.php",
                query,
                cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || payload.Status == false || payload.Rows is null || payload.Rows.Count == 0)
        {
            return [];
        }

        return payload.Rows
            .Select(r => new StorefrontPartOfferDigest(
                r.PriceId,
                r.PriceList ?? string.Empty,
                r.Manufacturer ?? string.Empty,
                r.Article ?? string.Empty,
                r.ArticleShow ?? string.Empty,
                r.Name ?? string.Empty,
                r.Price,
                r.Exist,
                r.Storage ?? string.Empty,
                r.TimeToExe ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<StorefrontArticleBrandDigest>> TryLoadBrandsAsync(
        string article,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetJsonAsync<PhpArticleBrandsPayload>(
                "/content/shop/docpart/ajax_epc_article_brands.php",
                new Dictionary<string, string?> { ["article"] = article },
                cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || payload.Status == false || payload.Manufacturers is null)
        {
            return [];
        }

        return payload.Manufacturers
            .Select(m => (m.ManufacturerShow ?? m.Manufacturer ?? string.Empty).Trim())
            .Where(b => b.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(b => new StorefrontArticleBrandDigest(b))
            .ToList();
    }

    /// <summary>
    /// POST proxy to PHP <c>ajax_getProductsOfBunch.php</c> (progressive supplier poll).
    /// Forwards the browser Cookie header so pricing identity matches PHP session.
    /// </summary>
    public async Task<StorefrontProductsOfBunchResult> TryLoadProductsOfBunchAsync(
        string article,
        string? brand,
        int officeId,
        int storageId,
        string? queryJson,
        int geoId = 0,
        CancellationToken cancellationToken = default)
    {
        var normalized = (article ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return new(0, officeId, storageId, [], false, "empty", "Article required.");
        }

        var searchObject = string.IsNullOrWhiteSpace(queryJson)
            ? JsonSerializer.Serialize(new
            {
                article = normalized,
                searsch_str = normalized,
                manufacturer = brand ?? string.Empty,
                manufacturers = Array.Empty<object>(),
                analogs = Array.Empty<object>()
            })
            : queryJson!;

        var form = new Dictionary<string, string>
        {
            ["geo_id"] = geoId.ToString(CultureInfo.InvariantCulture),
            ["office_id"] = officeId.ToString(CultureInfo.InvariantCulture),
            ["storage_id"] = storageId.ToString(CultureInfo.InvariantCulture),
            ["query"] = searchObject
        };

        var payload = await PostFormJsonAsync<PhpProductsOfBunchPayload>(
                "/content/shop/docpart/ajax_getProductsOfBunch.php",
                form,
                cancellationToken)
            .ConfigureAwait(false);

        if (payload is null)
        {
            return new(0, officeId, storageId, [], false, "php-empty", "Empty PHP bunch response.");
        }

        var products = (payload.Products ?? [])
            .Select(p => new StorefrontPartOfferDigest(
                0,
                p.StorageCaption ?? p.Storage ?? string.Empty,
                p.Manufacturer ?? string.Empty,
                p.Article ?? string.Empty,
                p.ArticleShow ?? string.Empty,
                p.Name ?? string.Empty,
                p.Price,
                p.Exist,
                p.StorageCaption ?? p.Storage ?? string.Empty,
                FormatJsonScalar(p.TimeToExe)))
            .ToList();

        return new(
            payload.Result,
            officeId,
            storageId,
            products,
            payload.PricesVisible,
            "php-bunch",
            payload.Message ?? string.Empty);
    }

    private async Task<T?> GetJsonAsync<T>(
        string path,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
        where T : class
    {
        var targets = BuildRequestTargets(path, query);
        if (targets.Count == 0)
        {
            return null;
        }

        var (client, dispose) = CreateClient();
        try
        {
            foreach (var target in targets)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, target.Uri);
                    ApplyTargetHeaders(request, target);

                    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var payload = await response.Content
                        .ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    if (payload is not null)
                    {
                        return payload;
                    }
                }
                catch
                {
                    // try next candidate base
                }
            }

            return null;
        }
        finally
        {
            if (dispose)
            {
                client.Dispose();
            }
        }
    }

    private async Task<T?> PostFormJsonAsync<T>(
        string path,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
        where T : class
    {
        var targets = BuildRequestTargets(path, new Dictionary<string, string?>());
        if (targets.Count == 0)
        {
            return null;
        }

        var (client, dispose) = CreateClient(timeoutSeconds: 45);
        try
        {
            foreach (var target in targets)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, target.Uri);
                    ApplyTargetHeaders(request, target);
                    ForwardBrowserCookies(request);
                    request.Content = new FormUrlEncodedContent(form);

                    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var payload = await response.Content
                        .ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    if (payload is not null)
                    {
                        return payload;
                    }
                }
                catch
                {
                    // try next candidate base
                }
            }

            return null;
        }
        finally
        {
            if (dispose)
            {
                client.Dispose();
            }
        }
    }

    private void ApplyTargetHeaders(HttpRequestMessage request, BridgeTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.HostHeader))
        {
            request.Headers.Host = target.HostHeader;
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private void ForwardBrowserCookies(HttpRequestMessage request)
    {
        var http = _httpContextAccessor?.HttpContext;
        if (http is null)
        {
            return;
        }

        if (http.Request.Headers.TryGetValue("Cookie", out var cookie) && !string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie.ToString());
        }
    }

    private List<BridgeTarget> BuildRequestTargets(string path, IReadOnlyDictionary<string, string?> query)
    {
        var http = _httpContextAccessor?.HttpContext;
        if (http is null)
        {
            return [];
        }

        var request = http.Request;
        var publicHost = request.Host.Host;
        if (string.IsNullOrWhiteSpace(publicHost))
        {
            return [];
        }

        var qs = string.Join(
            "&",
            query
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        var targets = new List<BridgeTarget>();

        // 1) Loopback + Host header — nginx vhost still serves PHP ajax for /content/shop/docpart/*
        // even when product HTML PHP serving is temporarily deactivated.
        foreach (var loopPort in new[] { request.Host.Port, 80, 8080 })
        {
            var builder = new UriBuilder
            {
                Scheme = "http",
                Host = "127.0.0.1",
                Path = path,
                Query = qs
            };
            if (loopPort is int p && p > 0 && p != 443)
            {
                builder.Port = p;
            }

            targets.Add(new BridgeTarget(builder.Uri, publicHost));
        }

        // 2) Public host (same scheme/port as incoming request) — works when outbound CF is allowed.
        var publicBuilder = new UriBuilder
        {
            Scheme = request.Scheme,
            Host = publicHost,
            Path = path,
            Query = qs
        };
        if (request.Host.Port is int publicPort)
        {
            publicBuilder.Port = publicPort;
        }

        targets.Add(new BridgeTarget(publicBuilder.Uri, HostHeader: null));
        return targets;
    }

    private (HttpClient Client, bool Dispose) CreateClient(int timeoutSeconds = 8)
    {
        if (_httpClientFactory is not null)
        {
            return (_httpClientFactory.CreateClient(nameof(PhpWarehouseSearchBridge)), false);
        }

        return (new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) }, true);
    }

    private static string FormatJsonScalar(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => string.Empty
        };

    private sealed record BridgeTarget(Uri Uri, string? HostHeader);

    private sealed class PhpWarehouseOffersPayload
    {
        public bool Status { get; set; }
        public List<PhpWarehouseOfferRow>? Rows { get; set; }
    }

    private sealed class PhpWarehouseOfferRow
    {
        [JsonPropertyName("price_id")]
        public int PriceId { get; set; }

        [JsonPropertyName("price_list")]
        public string? PriceList { get; set; }

        public string? Manufacturer { get; set; }
        public string? Article { get; set; }

        [JsonPropertyName("article_show")]
        public string? ArticleShow { get; set; }

        public string? Name { get; set; }
        public decimal Price { get; set; }
        public int Exist { get; set; }
        public string? Storage { get; set; }

        [JsonPropertyName("time_to_exe")]
        public string? TimeToExe { get; set; }
    }

    private sealed class PhpArticleBrandsPayload
    {
        public bool Status { get; set; }
        public List<PhpArticleBrandRow>? Manufacturers { get; set; }
    }

    private sealed class PhpArticleBrandRow
    {
        public string? Manufacturer { get; set; }

        [JsonPropertyName("manufacturer_show")]
        public string? ManufacturerShow { get; set; }
    }

    private sealed class PhpProductsOfBunchPayload
    {
        public int Result { get; set; }
        public string? Message { get; set; }
        public List<PhpBunchProductRow>? Products { get; set; }

        [JsonPropertyName("prices_visible")]
        public bool PricesVisible { get; set; } = true;
    }

    private sealed class PhpBunchProductRow
    {
        public string? Manufacturer { get; set; }
        public string? Article { get; set; }

        [JsonPropertyName("article_show")]
        public string? ArticleShow { get; set; }

        public string? Name { get; set; }
        public decimal Price { get; set; }
        public int Exist { get; set; }
        public string? Storage { get; set; }

        [JsonPropertyName("storage_caption")]
        public string? StorageCaption { get; set; }

        [JsonPropertyName("time_to_exe")]
        public JsonElement TimeToExe { get; set; }
    }
}
