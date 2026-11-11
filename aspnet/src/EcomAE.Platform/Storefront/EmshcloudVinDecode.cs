using System.Data.Common;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// tecdoc.emshcloud.com <c>/v1/vin</c> fallback: shared EcomAE subscription used only when
/// Laximo and partsapi both miss. Decodes a VIN, stashes the raw response into
/// <c>epc_umapi_vin_cache</c> so the page's cached path lights up, and returns the
/// standard decode result shape.
/// </summary>
public static class EmshcloudVinDecode
{
    private const string Endpoint = "https://tecdoc.emshcloud.com/v1/vin";

    public static async Task<LaximoVinDecodeResult> DecodeAndCacheAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        IErpWriteConnectionFactory connections,
        string vin,
        CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured || vin.Length != 17)
        {
            return new LaximoVinDecodeResult(false, "upstream", "VIN decode failed.", vin, null, null, 0, []);
        }

        string json;
        try
        {
            var key = await ApiKeyAsync(context, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(key))
            {
                return new LaximoVinDecodeResult(false, "upstream", "VIN decode failed.", vin, null, null, 0, []);
            }

            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint + "?sasi=" + Uri.EscapeDataString(vin) + "&dil=en");
            request.Headers.TryAddWithoutValidation("X-API-Key", key);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new LaximoVinDecodeResult(false, "upstream", "VIN decode failed.", vin, null, null, 0, []);
            }
        }
        catch (Exception)
        {
            return new LaximoVinDecodeResult(false, "upstream", "VIN decode failed.", vin, null, null, 0, []);
        }

        var manufacturer = "";
        var model = "";
        var vehicles = new List<LaximoVehicleRow>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("sonuc", out var sonuc)
                || sonuc.ValueKind != JsonValueKind.Object
                || !sonuc.TryGetProperty("araclar", out var araclar)
                || araclar.ValueKind != JsonValueKind.Array)
            {
                return new LaximoVinDecodeResult(false, "miss", "No vehicle for this VIN.", vin, null, null, 0, []);
            }

            foreach (var row in araclar.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Str(row, "arac_adi");
                var make = Str(row, "marka_ad");
                if (make.Length == 0 && name.Length > 0)
                {
                    make = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                }

                if (make.Length == 0 && name.Length == 0)
                {
                    continue;
                }

                vehicles.Add(new LaximoVehicleRow(make, name, "", ""));
                if (manufacturer.Length == 0)
                {
                    manufacturer = make;
                    model = name;
                }
            }
        }
        catch (JsonException)
        {
        }

        if (vehicles.Count == 0)
        {
            return new LaximoVinDecodeResult(false, "miss", "No vehicle for this VIN.", vin, null, null, 0, []);
        }

        var count = vehicles.Count;
        var cacheJson = JsonSerializer.Serialize(new
        {
            data = new
            {
                matchingManufacturers = new { array = new[] { new { manuName = manufacturer } } },
                matchingVehicles = new
                {
                    array = vehicles.Select(v => new { carName = (v.Brand + " " + v.Name).Trim(), vehicleTypeDescription = v.Name }).ToArray()
                },
                matchingVehiclesCount = count
            },
            status = 200
        });
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_umapi_vin_cache` (`vin`,`language`,`region`,`response_json`,`vehicle_count`,`manufacturer`,`model_label`,`http_status`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,UTC_TIMESTAMP()) ON DUPLICATE KEY UPDATE `response_json`=VALUES(`response_json`), `vehicle_count`=VALUES(`vehicle_count`), `manufacturer`=VALUES(`manufacturer`), `model_label`=VALUES(`model_label`), `http_status`=VALUES(`http_status`), `updated_at`=UTC_TIMESTAMP()"),
                cancellationToken,
                vin, "en", "WWW", cacheJson, count, manufacturer, model, 200).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        return new LaximoVinDecodeResult(true, "ok", "Decoded via emshcloud.", vin, manufacturer, model, count, vehicles);
    }

    /// <summary>Shared subscription key: per-host epc_settings.emshcloud_api_key, else global row.</summary>
    internal static async Task<string> ApiKeyAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var connections = context.RequestServices.GetService<ITenantDbConnectionFactory>();
        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (connections is not null && connections.IsConfigured)
        {
            try
            {
                await using var owned = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                var stored = await ErpDb.StringAsync(
                    owned,
                    null,
                    ErpDb.Positional(
                        "SELECT `setting_value` FROM `epc_settings` WHERE `setting_key` = 'emshcloud_api_key' "
                        + "AND (`site_key` = '' OR `site_key` IS NULL OR `site_key` = ?) "
                        + "ORDER BY CASE WHEN `site_key` IS NULL OR `site_key` = '' THEN 1 ELSE 0 END ASC LIMIT 1"),
                    cancellationToken,
                    context.Request.Host.Host).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(stored))
                {
                    return stored.Trim();
                }
            }
            catch (DbException)
            {
            }
        }

        // Shared-subscription fallback for tenants whose DB has no epc_settings.
        var config = context.RequestServices.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
        return config?["EcomAE:Emshcloud:Key"]?.Trim() ?? "";
    }

    private static string Str(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? v.ToString()
            : "";
}
