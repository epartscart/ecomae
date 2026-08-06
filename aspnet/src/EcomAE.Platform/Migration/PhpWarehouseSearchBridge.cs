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
                    if (!string.IsNullOrWhiteSpace(target.HostHeader))
                    {
                        request.Headers.Host = target.HostHeader;
                    }

                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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

    private (HttpClient Client, bool Dispose) CreateClient()
    {
        if (_httpClientFactory is not null)
        {
            return (_httpClientFactory.CreateClient(nameof(PhpWarehouseSearchBridge)), false);
        }

        return (new HttpClient { Timeout = TimeSpan.FromSeconds(8) }, true);
    }

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
}
