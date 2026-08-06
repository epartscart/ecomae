using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Loopback/public bridge to PHP CHPU warehouse endpoints that already use the shop DB via config.php.
/// Used when ASP.NET tenant SQL returns empty (wrong host binding / over-filters on older binaries).
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
        var uri = BuildUri(
            "/content/shop/docpart/ajax_epc_warehouse_offers.php",
            new Dictionary<string, string?>
            {
                ["article"] = article,
                ["brand"] = brand,
                ["limit"] = limit.ToString(CultureInfo.InvariantCulture)
            });
        if (uri is null)
        {
            return [];
        }

        try
        {
            var (client, dispose) = CreateClient();
            try
            {
                using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                var payload = await response.Content
                    .ReadFromJsonAsync<PhpWarehouseOffersPayload>(JsonOptions, cancellationToken)
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
            finally
            {
                if (dispose)
                {
                    client.Dispose();
                }
            }
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<StorefrontArticleBrandDigest>> TryLoadBrandsAsync(
        string article,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri(
            "/content/shop/docpart/ajax_epc_article_brands.php",
            new Dictionary<string, string?>
            {
                ["article"] = article
            });
        if (uri is null)
        {
            return [];
        }

        try
        {
            var (client, dispose) = CreateClient();
            try
            {
                using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                var payload = await response.Content
                    .ReadFromJsonAsync<PhpArticleBrandsPayload>(JsonOptions, cancellationToken)
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
            finally
            {
                if (dispose)
                {
                    client.Dispose();
                }
            }
        }
        catch
        {
            return [];
        }
    }

    private (HttpClient Client, bool Dispose) CreateClient()
    {
        if (_httpClientFactory is not null)
        {
            return (_httpClientFactory.CreateClient(nameof(PhpWarehouseSearchBridge)), false);
        }

        return (new HttpClient { Timeout = TimeSpan.FromSeconds(8) }, true);
    }

    private Uri? BuildUri(string path, IReadOnlyDictionary<string, string?> query)
    {
        var http = _httpContextAccessor?.HttpContext;
        if (http is null)
        {
            return null;
        }

        var request = http.Request;
        var builder = new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Path = path,
            Query = string.Join(
                "&",
                query
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"))
        };
        if (request.Host.Port is int port)
        {
            builder.Port = port;
        }

        // Prefer loopback to the same host when request came through public DNS —
        // Host header keeps vhost routing; avoids Cloudflare hop when possible.
        if (!string.Equals(builder.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            // Keep public host — PHP ajax is already proven on www.epartscart.com.
        }

        return builder.Uri;
    }

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
