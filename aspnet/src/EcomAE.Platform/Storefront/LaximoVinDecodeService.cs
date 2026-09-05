using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Configuration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live twin of PHP Guayaquil <c>FindVehicleByVIN</c> / <c>FindVehicleByFrameNo</c>
/// (<c>GuayaquilSoapWrapper.QueryDataLogin</c>). Cache-only lookup stays on
/// <c>ICatalogOfflineCacheService</c> when credentials are missing and the VIN is cached.
/// </summary>
public interface ILaximoVinDecodeService
{
    Task<LaximoVinDecodeResult> DecodeAsync(string? vinOrFrame, CancellationToken cancellationToken = default);
}

public sealed record LaximoVehicleRow(string Brand, string Name, string Catalog, string Ssd);

public sealed record LaximoVinDecodeResult(
    bool Ok,
    string Code,
    string Message,
    string Vin,
    string? Manufacturer,
    string? ModelLabel,
    int VehicleCount,
    IReadOnlyList<LaximoVehicleRow> Vehicles)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        surface = "storefront",
        writes = 0,
        phpAuthoritative = false,
        validation_code = Code,
        message = Message,
        vin = Vin,
        manufacturer = Manufacturer,
        model_label = ModelLabel,
        vehicle_count = VehicleCount,
        vehicles = Vehicles.Select(v => new { brand = v.Brand, name = v.Name, catalog = v.Catalog, ssd = v.Ssd }),
        session
    };
}

public sealed class LaximoVinDecodeService : ILaximoVinDecodeService
{
    public const string OemSoapUrl = "https://ws.laximo.net/ec.Kito.WebCatalog/services/Catalog.CatalogHttpSoap11Endpoint/";
    public const string DefaultLocale = "en_US";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IErpWriteConnectionFactory _connections;

    public LaximoVinDecodeService(
        IHttpClientFactory http,
        IConfiguration config,
        IErpWriteConnectionFactory connections)
    {
        _http = http;
        _config = config;
        _connections = connections;
    }

    public async Task<LaximoVinDecodeResult> DecodeAsync(string? vinOrFrame, CancellationToken cancellationToken = default)
    {
        var vin = NormalizeVin(vinOrFrame);
        if (vin.Length is < 11 or > 17)
        {
            return new LaximoVinDecodeResult(false, "invalid_vin", "Enter a VIN of 11–17 characters.", vin, null, null, 0, []);
        }

        var login = ResolveLogin();
        var key = ResolveKey();
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
