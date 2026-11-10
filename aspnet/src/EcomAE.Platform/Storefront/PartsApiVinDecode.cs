using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// partsapi.ru <c>VINdecode</c> fallback shared by the POST /storefront/vin/decode endpoint and
/// the vin-app page: decodes a VIN, stashes the raw response into <c>epc_umapi_vin_cache</c>
/// so the page's cached path lights up, and returns it in the Laximo result shape.
/// </summary>
public static class PartsApiVinDecode
{
    public static async Task<LaximoVinDecodeResult> DecodeAndCacheAsync(
        IPartsApiService partsApi,
        IErpWriteConnectionFactory connections,
        string vin,
        CancellationToken cancellationToken)
    {
        PartsApiResult result;
        try
        {
            result = await partsApi.ExecuteAsync(
                new PartsApiRequest("VINdecode",
                    new Dictionary<string, string> { ["vin"] = vin, ["lang"] = "en" }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new LaximoVinDecodeResult(false, "upstream", "VIN decode failed.", vin, null, null, 0, []);
        }

        if (!result.Ok)
        {
            return new LaximoVinDecodeResult(false, "upstream", "VIN decode failed.", vin, null, null, 0, []);
        }

        var manufacturer = "";
        var model = "";
        var count = 0;
        try
        {
            using var doc = JsonDocument.Parse(result.Json);
            var root = doc.RootElement;
            var row = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray().FirstOrDefault()
                : root;
            if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array)
            {
                row = data.EnumerateArray().FirstOrDefault();
            }

            if (row.ValueKind == JsonValueKind.Object)
            {
                string Field(params string[] names)
                {
                    foreach (var p in row.EnumerateObject())
                    {
                        if (names.Any(n => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))
                            && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                        {
                            return p.Value.ToString();
                        }
                    }

                    return "";
                }

                manufacturer = Field("make", "brand", "mark", "manufacturer", "maker", "makeName");
                model = Field("model", "modelName", "model_name", "carName", "modification", "vehicle");
                if (model.Length == 0)
                {
                    model = Field("name", "car", "typeName");
                }
            }

            count = root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : 1;
        }
        catch (JsonException)
        {
        }

        if (manufacturer.Length == 0 && model.Length == 0)
        {
            return new LaximoVinDecodeResult(false, "miss", "No vehicle for this VIN.", vin, null, null, 0, []);
        }

        var vehicles = new List<LaximoVehicleRow> { new(manufacturer, model, "", "") };
        var cacheJson = JsonSerializer.Serialize(new
        {
            data = new
            {
                matchingManufacturers = new { array = new[] { new { manuName = manufacturer } } },
                matchingVehicles = new
                {
                    array = new[] { new { carName = (manufacturer + " " + model).Trim(), vehicleTypeDescription = model } }
                },
                matchingVehiclesCount = count
            },
            status = 200
        });
        if (connections.IsConfigured)
        {
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
        }

        return new LaximoVinDecodeResult(true, "ok", "Decoded via partsapi.", vin, manufacturer, model, count, vehicles);
    }
}
