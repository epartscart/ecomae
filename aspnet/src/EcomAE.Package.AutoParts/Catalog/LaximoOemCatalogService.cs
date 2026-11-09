using System.Data.Common;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EcomAE.Package.AutoParts;

/// <summary>
/// OEM catalog drill-down (Guayaquil SOAP twin of api/laximo_proxy.php).
/// Shared platform credentials (epc_settings → EcomAE__Laximo__* env), per-tenant
/// auto-save cache in <c>epc_laximo_cache</c> — fetch once, serve stale when the
/// provider is unreachable.
/// </summary>
public sealed class LaximoOemCatalogService : ILaximoCatalogService
{
    private const int CacheTtlSeconds = 86400;

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPackageGate _packageGate;

    public LaximoOemCatalogService(
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

    public async Task<LaximoCatalogResult> ExecuteAsync(
        LaximoCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var siteKey = (httpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey]
            as TenantContext)?.SiteKey ?? string.Empty;
        if (siteKey.Length == 0
            || !await _packageGate.IsEnabledAsync(siteKey, AutoPartsPackage.PackageId, cancellationToken).ConfigureAwait(false))
        {
            return LaximoCatalogResult.Error(
                StatusCodes.Status503ServiceUnavailable, "package_disabled",
                "Laximo catalog service is unavailable for this storefront.");
        }

        var command = BuildCommand(request);
        if (command is null)
        {
            return LaximoCatalogResult.Error(
                StatusCodes.Status400BadRequest, "unknown_action",
                "Unknown action: " + request.Action);
        }

        var login = await ResolveSettingAsync("laximo_login", cancellationToken).ConfigureAwait(false)
            ?? _config["EcomAE:Laximo:Login"];
        var key = await ResolveSettingAsync("laximo_key", cancellationToken).ConfigureAwait(false)
            ?? _config["EcomAE:Laximo:Key"];
        var locale = _config["EcomAE:Laximo:Locale"];
        if (string.IsNullOrWhiteSpace(locale))
        {
            locale = LaximoVinDecodeService.DefaultLocale;
        }

        command = command.Replace("{locale}", locale, StringComparison.Ordinal);

        var cacheKey = NormalizeAction(request.Action) + "|" + command;
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(key))
        {
            var cachedCreds = await ReadCacheAsync(cacheKey, cancellationToken, allowExpired: true).ConfigureAwait(false);
            if (cachedCreds is not null)
            {
                return cachedCreds;
            }

            return LaximoCatalogResult.Error(
                StatusCodes.Status503ServiceUnavailable, "config",
                "Laximo credentials are not configured.");
        }

        var xml = await SendSoapAsync(command, login, key, cancellationToken).ConfigureAwait(false);
        if (xml is null)
        {
            var stale = await ReadCacheAsync(cacheKey, cancellationToken, allowExpired: true).ConfigureAwait(false);
            if (stale is not null)
            {
                return stale;
            }

            return LaximoCatalogResult.Error(
                StatusCodes.Status503ServiceUnavailable, "upstream",
                "Laximo service did not return a response.");
        }

        // SOAP Fault (e.g. E_ACCESSDENIED when the shared subscription is
        // disabled) — surface the provider's own message, not a generic error.
        var fault = ExtractFault(xml);
        if (fault is not null)
        {
            return LaximoCatalogResult.Error(
                StatusCodes.Status503ServiceUnavailable, "provider", fault);
        }

        var result = XmlToResult(request, xml);
        await WriteCacheAsync(cacheKey, request.Action, xml, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Guayaquil command per action; {locale} substituted at send time.</summary>
    public static string? BuildCommand(LaximoCatalogRequest request)
    {
        var loc = "{locale}";
        string? catalog = Esc(request.Catalog);
        string? ssd = Esc(request.Ssd);
        string? vehicleId = Esc(request.VehicleId);
        return request.Action switch
        {
            "catalogs" => $"ListCatalogs:Locale={loc}",
            "find_vehicle" => request.Vin is { Length: >= 11 and <= 17 }
                ? $"FindVehicleByVIN:Locale={loc}|Catalog=|VIN={Esc(request.Vin)}|ssd=|Localized=true"
                : $"FindVehicleByFrameNo:Locale={loc}|Catalog=|FrameNo={Esc(request.Vin ?? "")}|ssd=|Localized=true",
            "vehicle_info" => $"GetVehicleInfo:Locale={loc}|Catalog={catalog}|VehicleId={vehicleId}|ssd={ssd}|Localized=true",
            "wizard" => $"GetWizard2:Locale={loc}|Catalog={catalog}|ssd={ssd}",
            "wizard_next" => $"GetWizardNextStep2:Locale={loc}|Catalog={catalog}|ssd={ssd}|value={Esc(request.Value)}",
            "categories" => $"GetCategories:Locale={loc}|Catalog={catalog}|VehicleId={vehicleId}|ssd={ssd}|Localized=true",
            "units" => $"ListUnits:Locale={loc}|Catalog={catalog}|ssd={ssd}|CategoryId={Esc(request.CategoryId)}|Localized=true",
            "unit_details" => $"ListDetailByUnit:Locale={loc}|Catalog={catalog}|UnitId={Esc(request.UnitId)}|ssd={ssd}|Localized=true",
            "quick_groups" => $"ListQuickGroup:Locale={loc}|Catalog={catalog}|VehicleId={vehicleId}|ssd={ssd}",
            "quick_details" => $"ListQuickDetail:Locale={loc}|Catalog={catalog}|ssd={ssd}|QuickGroupId={Esc(request.GroupId)}|AllLevels=1|Path=",
            "part_search" => $"FindDetailsByArticle:Locale={loc}|Catalog={catalog}|Article={Esc(request.Query ?? request.Oem)}|Localized=true",
            "part_refs" => $"ListApplicableReplacements:Locale={loc}|Catalog={catalog}|DetailId={Esc(request.DetailId)}|ssd={ssd}|Localized=true",
            "applicability" => $"ListDetailApplicability:Locale={loc}|Catalog={catalog}|DetailId={Esc(request.DetailId)}|ssd={ssd}|Localized=true",
            "aftermarket" => $"FindDetailsByArticle:Locale={loc}|Catalog={catalog}|Article={Esc(request.Oem ?? request.Query)}|Localized=true",
            _ => null,
        };
    }

    private async Task<string?> SendSoapAsync(string command, string login, string key, CancellationToken cancellationToken)
    {
        var hmac = LaximoVinDecodeService.Md5Hex(command + key);
        var envelope = LaximoVinDecodeService.BuildSoapEnvelope(command, login, hmac);

        try
        {
            var client = _http.CreateClient(nameof(LaximoOemCatalogService));
            using var request = new HttpRequestMessage(HttpMethod.Post, LaximoVinDecodeService.OemSoapUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            // SOAP faults arrive as HTTP 500 with a valid Fault body — keep it so
            // the provider's message reaches the caller; only transport-level or
            // empty responses count as unreachable.
            return body.Length > 0 ? body : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Extracts a SOAP faultstring when the provider rejected the call.</summary>
    public static string? ExtractFault(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml) || !xml.Contains("Fault", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(xml);
            var fault = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("faultstring", StringComparison.OrdinalIgnoreCase));
            var text = fault?.Value?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes Guayaquil XML rows into the JSON contract laximo_storefront.js
    /// consumes ({success:true, data:[row-dicts]}). Row fields arrive as XML
    /// attributes; keys are lowercased for the JS field names.
    /// </summary>
    public static LaximoCatalogResult XmlToResult(LaximoCatalogRequest request, string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var rows = new List<Dictionary<string, string>>();
            foreach (var el in doc.Descendants())
            {
                var local = el.Name.LocalName;
                if (!local.Equals("row", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var attr in el.Attributes())
                {
                    dict[attr.Name.LocalName.ToLowerInvariant()] = attr.Value;
                }

                // Some responses nest fields as child elements instead of attributes.
                foreach (var child in el.Elements())
                {
                    var name = child.Name.LocalName.ToLowerInvariant();
                    if (!dict.ContainsKey(name))
                    {
                        dict[name] = child.Value;
                    }
                }

                if (dict.Count > 0)
                {
                    rows.Add(dict);
                }
            }

            // Friendly aliases the JS reads (unitid/oem/number/imageurl etc.) when the
            // provider uses alternate spellings.
            foreach (var row in rows)
            {
                Alias(row, "unitid", "unit_id", "id");
                Alias(row, "categoryid", "category_id", "id");
                Alias(row, "quickgroupid", "quick_group_id", "groupid", "id");
                Alias(row, "vehicleid", "vehicle_id", "id");
                Alias(row, "oem", "number", "code", "article", "oemnumber");
                Alias(row, "name", "title", "label");
                Alias(row, "imageurl", "image", "image_url", "picture");
                Alias(row, "note", "description", "comment");
                Alias(row, "catalog", "catalog_code", "catalogcode");
                Alias(row, "brand", "manufacturer", "mark");
            }

            var payload = JsonSerializer.Serialize(new
            {
                success = true,
                data = rows,
                count = rows.Count,
            });
            return LaximoCatalogResult.Payload(payload);
        }
        catch (System.Xml.XmlException)
        {
            return LaximoCatalogResult.Error(
                StatusCodes.Status502BadGateway, "parse", "Laximo response was not valid XML.");
        }
    }

    private static void Alias(Dictionary<string, string> row, string target, params string[] sources)
    {
        if (row.TryGetValue(target, out var existing) && existing.Length > 0)
        {
            return;
        }

        foreach (var source in sources)
        {
            if (row.TryGetValue(source, out var value) && value.Length > 0)
            {
                row[target] = value;
                return;
            }
        }
    }

    // Guayaquil passes ssd/catalog/detail tokens verbatim in the request string
    // (guayaquillib does not URL-encode them) — only trim, never re-encode.
    private static string Esc(string? value)
        => (value ?? string.Empty).Trim();

    private static string NormalizeAction(string action)
        => (action ?? string.Empty).Trim().ToLowerInvariant();

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

    private async Task EnsureCacheTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_laximo_cache` (
                `cache_key` VARCHAR(255) NOT NULL,
                `action` VARCHAR(40) NOT NULL DEFAULT '',
                `response_xml` MEDIUMTEXT,
                `http_status` INT NOT NULL DEFAULT 200,
                `last_sync` INT NOT NULL DEFAULT 0,
                PRIMARY KEY (`cache_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LaximoCatalogResult?> ReadCacheAsync(
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
            var xml = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `response_xml` FROM `epc_laximo_cache` WHERE `cache_key` = ? "
                    + (allowExpired ? "LIMIT 1" : "AND `last_sync` > ? LIMIT 1")),
                cancellationToken,
                cacheKey,
                cutoff).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(xml) ? null : XmlToResult(new LaximoCatalogRequest("cached"), xml);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private async Task WriteCacheAsync(string cacheKey, string action, string xml, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured || cacheKey.Length > 250)
        {
            return;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureCacheTableAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "REPLACE INTO `epc_laximo_cache` (`cache_key`, `action`, `response_xml`, `http_status`, `last_sync`) "
                    + "VALUES (?, ?, ?, 200, ?)"),
                cancellationToken,
                cacheKey,
                NormalizeAction(action),
                xml,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // cache is best-effort
        }
    }
}
