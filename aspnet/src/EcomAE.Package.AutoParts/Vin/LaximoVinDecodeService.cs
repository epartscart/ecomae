using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EcomAE.Package.AutoParts;

/// <summary>
/// Auto Parts industry package implementation of the VIN decode contract
/// (Guayaquil SOAP twin). Registered by <see cref="AutoPartsPackage"/>.
/// </summary>
public sealed class LaximoVinDecodeService : ILaximoVinDecodeService
{
    public const string OemSoapUrl = "https://ws.laximo.net/ec.Kito.WebCatalog/services/Catalog.CatalogHttpSoap11Endpoint/";
    public const string DefaultLocale = "en_US";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LaximoVinDecodeService(
        IHttpClientFactory http,
        IConfiguration config,
        IErpWriteConnectionFactory connections,
        IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _config = config;
        _connections = connections;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LaximoVinDecodeResult> DecodeAsync(string? vinOrFrame, CancellationToken cancellationToken = default)
    {
        var vin = NormalizeVin(vinOrFrame);
        if (vin.Length is < 11 or > 17)
        {
            return new LaximoVinDecodeResult(false, "invalid_vin", "Enter a VIN of 11–17 characters.", vin, null, null, 0, []);
        }

        var login = await ResolveLoginAsync(cancellationToken).ConfigureAwait(false);
        var key = await ResolveKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(key))
        {
            return new LaximoVinDecodeResult(false, "config", "Laximo credentials are not configured.", vin, null, null, 0, []);
        }

        var locale = _config["EcomAE:Laximo:Locale"];
        if (string.IsNullOrWhiteSpace(locale))
        {
            locale = DefaultLocale;
        }

        var command = vin.Length >= 17
            ? BuildFindVehicleByVin(vin, locale)
            : BuildFindVehicleByFrameNo(vin, locale);
        var hmac = Md5Hex(command + key);
        var envelope = BuildSoapEnvelope(command, login, hmac);

        using var request = new HttpRequestMessage(HttpMethod.Post, OemSoapUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        string body;
        try
        {
            var client = _http.CreateClient(nameof(LaximoVinDecodeService));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new LaximoVinDecodeResult(false, "upstream", "Laximo decode failed.", vin, null, null, 0, []);
            }
        }
        catch (HttpRequestException)
        {
            return new LaximoVinDecodeResult(false, "upstream", "Laximo decode failed.", vin, null, null, 0, []);
        }

        var vehicles = ParseVehicles(body);
        var manufacturer = vehicles.Count > 0 ? vehicles[0].Brand : "";
        var model = vehicles.Count > 0 ? vehicles[0].Name : "";
        if (vehicles.Count == 0)
        {
            return new LaximoVinDecodeResult(false, "miss", "No vehicle for this VIN.", vin, null, null, 0, []);
        }

        await TryCacheAsync(vin, manufacturer, model, vehicles.Count, body, cancellationToken).ConfigureAwait(false);
        return new LaximoVinDecodeResult(true, "ok", "Decoded", vin, manufacturer, model, vehicles.Count, vehicles);
    }

    public static string BuildFindVehicleByVin(string vin, string locale)
        => "FindVehicleByVIN:Locale=" + locale + "|Catalog=|VIN=" + vin + "|ssd=|Localized=true";

    public static string BuildFindVehicleByFrameNo(string frameNo, string locale)
        => "FindVehicleByFrameNo:Locale=" + locale + "|Catalog=|FrameNo=" + frameNo + "|ssd=|Localized=true";

    public static string Md5Hex(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static string BuildSoapEnvelope(string requestXml, string login, string hmac)
    {
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var cat = XNamespace.Get("http://WebCatalog.Kito.ec");
        var envelope = new XDocument(
            new XElement(ns + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", ns.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cat", cat.NamespaceName),
                new XElement(ns + "Body",
                    new XElement(cat + "QueryDataLogin",
                        new XElement(cat + "request", requestXml),
                        new XElement(cat + "login", login),
                        new XElement(cat + "hmac", hmac)))));
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + envelope.ToString(SaveOptions.DisableFormatting);
    }

    internal static string NormalizeVin(string? raw)
        => (raw ?? string.Empty).Trim().ToUpperInvariant();

    public static IReadOnlyList<LaximoVehicleRow> ParseVehicles(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        try
        {
            var doc = XDocument.Parse(xml);
            var rows = new List<LaximoVehicleRow>();
            foreach (var el in doc.Descendants())
            {
                var local = el.Name.LocalName;
                if (!local.Equals("row", StringComparison.OrdinalIgnoreCase)
                    && !local.Equals("vehicle", StringComparison.OrdinalIgnoreCase)
                    && !local.Equals("Vehicle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string Attr(params string[] names)
                {
                    foreach (var name in names)
                    {
                        var value = (string?)el.Attribute(name);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value.Trim();
                        }
                    }

                    return "";
                }

                var brand = Attr("brand", "Brand", "manufacturer", "mark");
                var name = Attr("name", "Name", "model", "title");
                var catalog = Attr("catalog", "Catalog");
                var ssd = Attr("ssd", "SSD");
                if (brand.Length == 0 && name.Length == 0)
                {
                    continue;
                }

                rows.Add(new LaximoVehicleRow(brand, name, catalog, ssd));
            }

            return rows;
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }
    }

    private async Task<string> ResolveLoginAsync(CancellationToken cancellationToken)
    {
        var stored = await StoredSettingAsync("laximo_login", cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(stored) ? stored.Trim() : ResolveLogin();
    }

    private async Task<string> ResolveKeyAsync(CancellationToken cancellationToken)
    {
        var stored = await StoredSettingAsync("laximo_key", cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(stored) ? stored.Trim() : ResolveKey();
    }

    /// <summary>Per-host credential from epc_settings (CP industry settings), with '' global fallback.</summary>
    private async Task<string?> StoredSettingAsync(string settingKey, CancellationToken cancellationToken)
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
            // epc_settings may not exist yet; env/config fallbacks apply.
            return null;
        }
    }

    private string ResolveLogin()
    {
        var login = _config["EcomAE:Laximo:Login"];
        if (!string.IsNullOrWhiteSpace(login))
        {
            return login.Trim();
        }

        var env = Environment.GetEnvironmentVariable("UUE_CUSTOMER_LOGIN");
        if (string.IsNullOrWhiteSpace(env))
        {
            return "";
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(env)).Trim();
        }
        catch (FormatException)
        {
            return env.Trim();
        }
    }

    private string ResolveKey()
    {
        var key = _config["EcomAE:Laximo:Key"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key.Trim();
        }

        var env = Environment.GetEnvironmentVariable("UUE_CUSTOMER_PASSWORD");
        if (string.IsNullOrWhiteSpace(env))
        {
            return "";
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(env)).Trim();
        }
        catch (FormatException)
        {
            return env.Trim();
        }
    }

    private async Task TryCacheAsync(
        string vin,
        string manufacturer,
        string model,
        int count,
        string rawXml,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_umapi_vin_cache` (`vin`,`language`,`region`,`response_json`,`vehicle_count`,`manufacturer`,`model_label`,`http_status`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,UTC_TIMESTAMP()) ON DUPLICATE KEY UPDATE `response_json`=VALUES(`response_json`), `vehicle_count`=VALUES(`vehicle_count`), `manufacturer`=VALUES(`manufacturer`), `model_label`=VALUES(`model_label`), `http_status`=VALUES(`http_status`), `updated_at`=UTC_TIMESTAMP()"),
                cancellationToken,
                vin, "en", "WWW", rawXml, count, manufacturer, model, 200).ConfigureAwait(false);
        }
        catch
        {
            // Cache table is optional; live decode still returns vehicles.
        }
    }
}
