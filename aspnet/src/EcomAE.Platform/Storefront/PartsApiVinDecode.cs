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
        var vehicles = new List<LaximoVehicleRow>();
        try
        {
            using var doc = JsonDocument.Parse(result.Json);
            var root = doc.RootElement;
            var rows = new List<JsonElement>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in root.EnumerateArray())
                {
                    rows.Add(r);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in data.EnumerateArray())
                {
                    rows.Add(r);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                rows.Add(root);
            }

            // Flatten envelope wrappers ("data"/"result" as array or as an
            // object-of-objects like {"result":{"0":{vehicle},...}}) into
            // their vehicle rows.
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var unwrapped = new List<JsonElement>();
                foreach (var key in new[] { "data", "result" })
                {
                    if (!rows[i].TryGetProperty(key, out var inner))
                    {
                        continue;
                    }

                    if (inner.ValueKind == JsonValueKind.Array)
                    {
                        unwrapped.AddRange(inner.EnumerateArray());
                    }
                    else if (inner.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in inner.EnumerateObject())
                        {
                            unwrapped.Add(p.Value);
                        }
                    }
                }

                if (unwrapped.Count > 0)
                {
                    rows.RemoveAt(i);
                    rows.InsertRange(i, unwrapped);
                    i--;
                }
            }

            foreach (var row in rows)
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

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

                var mk = Field("make", "brand", "mark", "manufacturer", "maker", "makeName", "manuName", "manuShortName");
                var md = Field("model", "modelName", "model_name", "carName", "modification", "vehicle");
                var type = Field("typeName", "name", "car");
                if (md.Length > 0 && type.Length > 0 && !md.Contains(type, StringComparison.OrdinalIgnoreCase))
                {
                    md = (md + " " + type).Trim();
                }
                else if (md.Length == 0)
                {
                    md = type;
                }

                if (mk.Length == 0 && md.Length == 0)
                {
                    continue;
                }

                vehicles.Add(new LaximoVehicleRow(mk, md, "", ""));
                if (manufacturer.Length == 0)
                {
                    manufacturer = mk;
                    model = md;
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
