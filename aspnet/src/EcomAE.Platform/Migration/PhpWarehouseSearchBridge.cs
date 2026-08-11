using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Api.Catalog;

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
    /// Load office/storage bunches from PHP <c>ajax_epc_office_storage_bunches.php</c>
    /// (same construction as <c>part_search_page.php</c>).
    /// </summary>
    public async Task<IReadOnlyList<StorefrontOfficeStorageBunchDigest>> TryLoadBunchesAsync(
        string article,
        string? brand,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetJsonAsync<PhpOfficeStorageBunchesPayload>(
                "/content/shop/docpart/ajax_epc_office_storage_bunches.php",
                new Dictionary<string, string?>
                {
                    ["article"] = article,
                    ["brand"] = brand
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || payload.Status == false || payload.Bunches is null || payload.Bunches.Count == 0)
        {
            return [];
        }

        return payload.Bunches
            .Select(MapPhpBunch)
            .Where(b => b is not null)
            .Select(b => b!)
            .ToList();
    }

    private static StorefrontOfficeStorageBunchDigest? MapPhpBunch(PhpOfficeStorageBunchRow row)
    {
        // Skip async cross-server sentinel (protocol_version: "server").
        if (row.ProtocolVersion.ValueKind == JsonValueKind.String
            && string.Equals(row.ProtocolVersion.GetString(), "server", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var protocol = 1;
        if (row.ProtocolVersion.ValueKind == JsonValueKind.Number)
        {
            protocol = row.ProtocolVersion.GetInt32();
        }
        else if (row.ProtocolVersion.ValueKind == JsonValueKind.String
                 && int.TryParse(row.ProtocolVersion.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            protocol = parsed;
        }

        IReadOnlyList<StorefrontOfficeStorageBunchDigest>? nested = null;
        if (row.OfficeStorageBunches is { Count: > 0 })
        {
            nested = row.OfficeStorageBunches
                .Select(MapPhpBunch)
                .Where(b => b is not null)
                .Select(b => b!)
                .ToList();
        }

        return new StorefrontOfficeStorageBunchDigest(
            row.OfficeId,
            row.StorageId,
            protocol,
            protocol == 3 ? "prices" : string.Empty,
            row.TreelaxCatalogue,
            nested);
    }

    /// <summary>
    /// PHP <c>ajax_epc_cross_search.php</c> — CP crosses + crossbase + stock enrichment.
    /// Local <c>shop_docpart_articles_analogs_list</c> alone misses most CHPU OE networks
    /// (e.g. JSASAKASHI/C110J → 600+ references).
    /// </summary>
    public async Task<IReadOnlyList<StorefrontCrossRefDigest>> TryLoadCrossSearchAsync(
        string article,
        string? brand,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalized = (article ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        var query = new Dictionary<string, string?>
        {
            ["article"] = normalized
        };
        if (!string.IsNullOrWhiteSpace(brand))
        {
            query["brand"] = brand.Trim();
        }

        // Live crossbase expansion regularly exceeds the short warehouse-offer timeout.
        var payload = await GetJsonAsync<PhpCrossSearchPayload>(
                "/content/shop/docpart/ajax_epc_cross_search.php",
                query,
                cancellationToken,
                timeoutSeconds: 60)
            .ConfigureAwait(false);

        if (payload is null || payload.Status == false)
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit, 1, 500);
        var stockKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stock in payload.Stock ?? [])
        {
            var sb = (stock.Brand ?? string.Empty).Trim();
            var sa = PriceLookupRequest.NormalizeArticle(stock.ArticleNorm ?? stock.Article ?? string.Empty);
            if (sb.Length > 0 && sa.Length > 0)
            {
                stockKeys.Add(sb.ToUpperInvariant() + "|" + sa);
            }
        }

        var rows = new List<StorefrontCrossRefDigest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selfArticle = PriceLookupRequest.NormalizeArticle(normalized);
        var selfBrand = CompactBrand(brand);
        foreach (var reference in payload.References ?? [])
        {
            var refBrand = (reference.Brand ?? string.Empty).Trim();
            var refArticle = (reference.Article ?? string.Empty).Trim();
            var refNorm = PriceLookupRequest.NormalizeArticle(reference.ArticleNorm ?? refArticle);
            if (refBrand.Length == 0 || refNorm.Length == 0)
            {
                continue;
            }

            // Skip the searched brand+article itself.
            if (refNorm == selfArticle && CompactBrand(refBrand) == selfBrand)
            {
                continue;
            }

            var key = refBrand.ToUpperInvariant() + "|" + refNorm;
            if (!seen.Add(key))
            {
                continue;
            }

            rows.Add(new StorefrontCrossRefDigest(refBrand, refArticle.Length > 0 ? refArticle : refNorm, stockKeys.Contains(key)));
            if (rows.Count >= safeLimit)
            {
                break;
            }
        }

        return rows;
    }

    private static string CompactBrand(string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(brand.Length);
        foreach (var ch in brand)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
        }

        return sb.ToString();
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
                FormatJsonScalar(p.TimeToExe),
                p.CheckHash ?? string.Empty,
                p.MinOrder > 0 ? p.MinOrder : 1,
                p.ProductType > 0 ? p.ProductType : 2,
                p.OfficeId,
                p.StorageId,
                p.JsonParams ?? string.Empty,
                p.PricePurchase,
                p.Markup,
                p.Probability > 0 ? p.Probability : 100,
                FormatJsonScalar(p.TimeToExeGuaranteed)))
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
        CancellationToken cancellationToken,
        int timeoutSeconds = 8)
        where T : class
    {
        var targets = BuildRequestTargets(path, query);
        if (targets.Count == 0)
        {
            return null;
        }

        var (client, dispose) = CreateClient(timeoutSeconds);
        try
        {
            // Named factory clients may ignore CreateClient timeout — pin for long cross searches.
            if (!dispose && client.Timeout < TimeSpan.FromSeconds(timeoutSeconds))
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            }

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

    private sealed class PhpCrossSearchPayload
    {
        public bool Status { get; set; }
        public List<PhpCrossReferenceRow>? References { get; set; }
        public List<PhpCrossStockRow>? Stock { get; set; }

        [JsonPropertyName("unique_reference_count")]
        public int UniqueReferenceCount { get; set; }

        [JsonPropertyName("reference_count")]
        public int ReferenceCount { get; set; }

        [JsonPropertyName("stock_count")]
        public int StockCount { get; set; }
    }

    private sealed class PhpCrossReferenceRow
    {
        public string? Brand { get; set; }
        public string? Article { get; set; }

        [JsonPropertyName("article_norm")]
        public string? ArticleNorm { get; set; }

        public string? Name { get; set; }
        public string? Source { get; set; }
    }

    private sealed class PhpCrossStockRow
    {
        public string? Brand { get; set; }
        public string? Article { get; set; }

        [JsonPropertyName("article_norm")]
        public string? ArticleNorm { get; set; }

        public string? Name { get; set; }
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

    private sealed class PhpOfficeStorageBunchesPayload
    {
        public bool Status { get; set; }
        public List<PhpOfficeStorageBunchRow>? Bunches { get; set; }
    }

    private sealed class PhpOfficeStorageBunchRow
    {
        [JsonPropertyName("office_id")]
        public int OfficeId { get; set; }

        [JsonPropertyName("storage_id")]
        public int StorageId { get; set; }

        [JsonPropertyName("protocol_version")]
        public JsonElement ProtocolVersion { get; set; }

        [JsonPropertyName("treelax_catalogue")]
        public bool TreelaxCatalogue { get; set; }

        [JsonPropertyName("office_storage_bunches")]
        public List<PhpOfficeStorageBunchRow>? OfficeStorageBunches { get; set; }
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

        [JsonPropertyName("time_to_exe_guaranteed")]
        public JsonElement TimeToExeGuaranteed { get; set; }

        [JsonPropertyName("check_hash")]
        public string? CheckHash { get; set; }

        [JsonPropertyName("min_order")]
        public int MinOrder { get; set; } = 1;

        [JsonPropertyName("product_type")]
        public int ProductType { get; set; } = 2;

        [JsonPropertyName("office_id")]
        public int OfficeId { get; set; }

        [JsonPropertyName("storage_id")]
        public int StorageId { get; set; }

        [JsonPropertyName("json_params")]
        public string? JsonParams { get; set; }

        [JsonPropertyName("price_purchase")]
        public decimal PricePurchase { get; set; }

        public int Markup { get; set; }

        public int Probability { get; set; } = 100;
    }
}
