using System.Data.Common;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EcomAE.Package.AutoParts;

/// <summary>
/// TecDoc catalog via partsapi.ru (REST twin of the umapi/laximo flows).
/// One shared EcomAE-level API key (epc_settings → EcomAE__PartsApi__* env) is
/// reused by every auto-parts tenant; each successful fetch is auto-saved into
/// the tenant's own <c>epc_partsapi_cache</c> — fetch once, serve stale when the
/// provider is unreachable.
/// </summary>
public sealed class PartsApiCatalogService : IPartsApiService
{
    private const int CacheTtlSeconds = 86400;
    private const string DefaultBaseUrl = "https://partsapi.ru/api";

    /// <summary>Read-only methods the proxy is allowed to call.</summary>
    public static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "getMakes", "getModels", "getCars", "getSearchTree", "getArticles", "getArticle",
        "getArticleMedia", "getArticleCrosses", "getArticleCriteria",
        "getApplicability", "getApplicability2", "getFullInfoCV",
        "searchArticles", "tecdocCrosses", "getEngine", "getPassengerCarInfo",
        "getProductGroupsByBrandNumber",
        "VINdecode", "VINdecodeOE", "decodeVINus", "getPartsbyVIN",
        "GetNormsMakes", "GetNormsModels", "GetNormsMotors", "GetNormsTimes", "GetFillVolumes",
        "toMakes", "toModels", "toTypes", "toParts", "toDopusk", "toDoPass", "toOils",
        "FindEAN13", "DecodeEAN13", "PartSuggest", "partNameSuggest",
        "getCrosses", "getCrossesTitle", "getCrossesWithBrand",
        "getPartnameByBrandNumber", "getPartWeight", "getCarsByOilSpecification",
        "gosnomer2vin", "carImpoundment", "rusNameSuggest",
    };

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPackageGate _packageGate;

    public PartsApiCatalogService(
        IHttpClientFactory http,
        IConfiguration config,
        IErpWriteConnectionFactory connections,
        IHttpContextAccessor httpContextAccessor,
        IPackageGate packageGate)
    {
        _http = http;
        _config = config;
        _connections = connections;
        _httpContextAccessor = httpContextAccessor;
        _packageGate = packageGate;
    }

    public async Task<PartsApiResult> ExecuteAsync(
        PartsApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var siteKey = (httpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey]
            as TenantContext)?.SiteKey ?? string.Empty;
        if (siteKey.Length == 0
            || !await _packageGate.IsEnabledAsync(siteKey, AutoPartsPackage.PackageId, cancellationToken).ConfigureAwait(false))
        {
            return PartsApiResult.Error(
                StatusCodes.Status503ServiceUnavailable, "package_disabled",
                "partsapi.ru catalog service is unavailable for this storefront.");
        }

        var method = (request.Method ?? string.Empty).Trim();
        if (!AllowedMethods.Contains(method))
        {
            return PartsApiResult.Error(
                StatusCodes.Status400BadRequest, "unknown_method",
                "Unknown or disallowed partsapi method: " + method);
        }

        // partsapi.ru issues a separate key per purchased method, so a
        // method-scoped setting/env wins, then the shared tenant key, then env.
        var key = await ResolveSettingAsync("partsapi_key_" + method.ToLowerInvariant(), cancellationToken).ConfigureAwait(false)
            ?? _config["EcomAE:PartsApi:MethodKeys:" + method]
            ?? _config["EcomAE:PartsApi:MethodKeys:" + method.ToLowerInvariant()]
            ?? await ResolveSettingAsync("partsapi_key", cancellationToken).ConfigureAwait(false)
            ?? _config["EcomAE:PartsApi:Key"];
        var baseUrl = _config["EcomAE:PartsApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        var cacheKey = BuildCacheKey(method, request.Params);

        // Fetch-once: serve a fresh cached response before spending a provider call.
        var fresh = await ReadCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (fresh is not null)
        {
            return fresh;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            var cachedCreds = await ReadCacheAsync(cacheKey, cancellationToken, allowExpired: true).ConfigureAwait(false);
            if (cachedCreds is not null)
            {
                return cachedCreds;
            }

            return PartsApiResult.Error(
                StatusCodes.Status503ServiceUnavailable, "config",
                "partsapi.ru API key is not configured.");
        }

        var upstream = await SendAsync(baseUrl, method, key, request.Params, cancellationToken).ConfigureAwait(false);
        if (upstream.Body is null)
        {
            var stale = await ReadCacheAsync(cacheKey, cancellationToken, allowExpired: true).ConfigureAwait(false);
            if (stale is not null)
            {
                return stale;
            }

            return PartsApiResult.Error(
                StatusCodes.Status503ServiceUnavailable, "upstream",
                "partsapi.ru did not return a response.");
        }

        // Non-2xx or an error payload ({"code":9000}, {"error_code":5002},
        // {"status":4xx}) — surface the provider's own message, never cache it,
        // and serve a stale row when one exists.
        var providerError = upstream.Status >= 400
            ? ExtractProviderError(upstream.Body) ?? "partsapi.ru returned HTTP " + upstream.Status + "."
            : ExtractProviderError(upstream.Body);
        if (providerError is not null)
        {
            var stale = await ReadCacheAsync(cacheKey, cancellationToken, allowExpired: true).ConfigureAwait(false);
            if (stale is not null)
            {
                return stale;
            }

            return PartsApiResult.Error(
                StatusCodes.Status503ServiceUnavailable, "provider", providerError);
        }

        var stored = WrapBody(method, upstream.Body);
        await WriteCacheAsync(cacheKey, method, request.Params, stored, cancellationToken).ConfigureAwait(false);
        return PartsApiResult.Payload(stored);
    }

    /// <summary>
    /// Shared cache key, identical to the existing epc_partsapi_cache producer:
    /// sha1(method + "|" + canonical params JSON with keys sorted).
    /// </summary>
    public static string BuildCacheKey(string method, IReadOnlyDictionary<string, string>? parameters)
    {
        var builder = new StringBuilder(method);
        builder.Append('|').Append(CanonicalParamsJson(parameters));
        var hash = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }

    public static string CanonicalParamsJson(IReadOnlyDictionary<string, string>? parameters)
    {
        var ordered = (parameters ?? new Dictionary<string, string>())
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(p => p.Key, p => p.Value ?? string.Empty);
        return JsonSerializer.Serialize(ordered);
    }

    /// <summary>
    /// Returns the provider's error message when the body is an error payload
    /// ({"code":9000}, {"error_code":5002} or HTTP-style {"status":4xx});
    /// null when the body is a normal data payload.
    /// </summary>
    public static string? ExtractProviderError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = doc.RootElement;
            var isError = false;
            if (root.TryGetProperty("error_code", out var errorCode)
                && errorCode.ValueKind == JsonValueKind.Number
                && errorCode.GetInt32() >= 4000)
            {
                isError = true;
            }
            else if (root.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.Number
                && code.GetInt32() >= 4000)
            {
                isError = true;
            }
            else if (root.TryGetProperty("status", out var status)
                && status.ValueKind == JsonValueKind.Number
                && status.GetInt32() >= 400)
            {
                isError = true;
            }

            if (!isError)
            {
                return null;
            }

            return root.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
                ? message.GetString() ?? "partsapi.ru returned an error."
                : "partsapi.ru returned an error.";
        }
        catch (JsonException)
        {
            // Non-JSON body is treated as an upstream failure by the caller.
            return "partsapi.ru returned a non-JSON response.";
        }
    }

    public sealed record UpstreamResponse(int Status, string? Body);

    private async Task<UpstreamResponse> SendAsync(
        string baseUrl,
        string method,
        string key,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        var url = new StringBuilder(baseUrl)
            .Append("?method=").Append(Uri.EscapeDataString(method))
            .Append("&key=").Append(Uri.EscapeDataString(key));
        if (parameters is not null)
        {
            foreach (var pair in parameters)
            {
                if (pair.Key.Length == 0 || pair.Key.Length > 64)
                {
                    continue;
                }

                url.Append('&')
                    .Append(Uri.EscapeDataString(pair.Key)).Append('=')
                    .Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
            }
        }

        try
        {
            var client = _http.CreateClient(nameof(PartsApiCatalogService));
            using var response = await client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            // Keep error payloads (401 with {"error_code":...}) so the provider's
            // message reaches the caller; only transport-level or empty bodies
            // count as unreachable.
            if (body.Length == 0)
            {
                return new UpstreamResponse((int)response.StatusCode, null);
            }

            return new UpstreamResponse((int)response.StatusCode, body);
        }
        catch (HttpRequestException)
        {
            return new UpstreamResponse(0, null);
        }
        catch (TaskCanceledException)
        {
            return new UpstreamResponse(0, null);
        }
    }

    private async Task<string?> ResolveSettingAsync(string settingKey, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            var host = _httpContextAccessor.HttpContext?.Request.Host.Host ?? string.Empty;
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `setting_value` FROM `epc_settings` WHERE `setting_key` = ? "
                    + "AND (`site_key` = '' OR `site_key` IS NULL OR `site_key` = ?) "
                    + "ORDER BY CASE WHEN `site_key` IS NULL OR `site_key` = '' THEN 1 ELSE 0 END ASC LIMIT 1"),
                cancellationToken,
                settingKey,
                host).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>Wraps a provider payload in the cache's canonical envelope.</summary>
    public static string WrapBody(string method, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("ok", out _))
            {
                return body; // already wrapped
            }

            var count = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength() : 1;
            return JsonSerializer.Serialize(new
            {
                ok = true,
                action = method.ToLowerInvariant(),
                rows = count,
                data = doc.RootElement.Clone(),
            });
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private async Task EnsureCacheTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_partsapi_cache` (
                `cache_key` VARCHAR(190) NOT NULL,
                `method` VARCHAR(64) NOT NULL,
                `action` VARCHAR(40) NOT NULL DEFAULT '',
                `section` VARCHAR(20) NOT NULL DEFAULT '',
                `language` VARCHAR(10) NOT NULL DEFAULT 'en',
                `request_json` TEXT,
                `response_json` MEDIUMTEXT NOT NULL,
                `rows_count` INT NOT NULL DEFAULT 0,
                `http_status` INT NOT NULL DEFAULT 200,
                `last_sync` INT NOT NULL DEFAULT 0,
                PRIMARY KEY (`cache_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<PartsApiResult?> ReadCacheAsync(
        string cacheKey, CancellationToken cancellationToken, bool allowExpired = false)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureCacheTableAsync(connection, cancellationToken).ConfigureAwait(false);
            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - CacheTtlSeconds;
            var json = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `response_json` FROM `epc_partsapi_cache` WHERE `cache_key` = ? "
                    + (allowExpired ? "LIMIT 1" : "AND `last_sync` > ? LIMIT 1")),
                cancellationToken,
                cacheKey,
                cutoff).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(json) ? null : PartsApiResult.Payload(json);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private async Task WriteCacheAsync(
        string cacheKey, string method, IReadOnlyDictionary<string, string>? parameters,
        string json, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured || cacheKey.Length > 250)
        {
            return;
        }

        try
        {
            var rows = 0;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("rows", out var r)
                    && r.ValueKind == JsonValueKind.Number)
                {
                    rows = r.GetInt32();
                }
            }
            catch (JsonException)
            {
            }

            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureCacheTableAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "REPLACE INTO `epc_partsapi_cache` (`cache_key`, `method`, `action`, `section`, `language`, `request_json`, `response_json`, `rows_count`, `http_status`, `last_sync`) "
                    + "VALUES (?, ?, ?, 'catalog', ?, ?, ?, ?, 200, ?)"),
                cancellationToken,
                cacheKey,
                method,
                method.ToLowerInvariant(),
                (parameters is not null
                    && (parameters.TryGetValue("lang", out var lang) || parameters.TryGetValue("LANG", out lang))
                    && lang is not null)
                    ? lang : "en",
                CanonicalParamsJson(parameters),
                json,
                rows,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // cache is best-effort
        }
    }
}
