using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string MultivendorFileStaysClassic = "File ingest stays on the classic importer.";
    public const string MultivendorChooseFile = "Choose an Excel/CSV file with multiple vendors";

    /// <summary>PHP <c>epc_multivendor_sample_csv</c> via <c>fputcsv</c>.</summary>
    public const string MultivendorSampleCsv =
        """"
        Brand,Article,Name,Qty,Price,"Vendor full name","Vendor short","Data type",Delivery,"Engine code","Country code",Size,"Cross reference","OE number","Other information"
        TOYOTA,446610010,"PAD KIT, DISC BRAKE",8,103.51,"S-UAE Trading LLC",S-UAE,inventory,0,2JZGE,JP,"15""",04465-YZZD2,044650K090,"Ceramic; front"
        AISIN,DT068,"WATER PUMP",3,45.00,"R-UAE Spare Parts FZE",R-UAE,inventory,0,1KZTE,TH,STD,16100-69355,1610069355,
        BOSCH,F026400039,FILTER,4,15.00,"Gulf Parts Trading",S-UAE,inventory,0,,DE,OE,0986AF0078,,"Oil filter"
        DENSO,0671007450,FILTER,12,18.00,"S-UAE Trading LLC",S-UAE,sales,0,3L,JP,,,,
        DENSO,0671007450,FILTER,5,22.50,"S-UAE Trading LLC",S-UAE,sales,0,3L,JP,,,,
        DENSO,0671007450,FILTER,2,29.90,"S-UAE Trading LLC",S-UAE,sales,0,3L,JP,,,,
        """";

    public static Task<object> MultivendorIngestAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> fields,
        ICpPricesUploadWriteService prices,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (adminId, token) => MultivendorBodyAsync(connection, adminId, fields, prices, token),
            cancellationToken);

    private static async Task<object> MultivendorBodyAsync(
        DbConnection connection,
        int adminId,
        IReadOnlyDictionary<string, string> fields,
        ICpPricesUploadWriteService prices,
        CancellationToken cancellationToken)
    {
        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action.Length == 0)
        {
            action = "upload";
        }

        if (action == "sample")
        {
            var format = OmsField(fields, "format").Trim().ToLowerInvariant();
            var download = OmsField(fields, "download");
            var wantFile = (download.Length > 0 && download != "0") || format == "csv";
            if (wantFile)
            {
                return new RawHttp(MultivendorSampleCsv, "text/csv; charset=utf-8");
            }

            return new JsonObject
            {
                ["status"] = true,
                ["filename"] = "epc-multivendor-sample.csv",
                ["csv"] = MultivendorSampleCsv,
            };
        }

        if (action == "min_price_acl_get")
        {
            return await MinPriceAclPayloadAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        if (action == "min_price_acl_save")
        {
            var written = await prices.SaveMinPriceAclAsync(
                First(fields, "restrict", "restrict_min"),
                First(fields, "group_ids", "group_ids_json"),
                First(fields, "user_ids", "user_ids_json"),
                adminId,
                cancellationToken).ConfigureAwait(false);
            if (!written.Succeeded)
            {
                return new FlagBody(false, written.Message);
            }

            var payload = await MinPriceAclPayloadAsync(connection, cancellationToken).ConfigureAwait(false);
            payload["message"] = written.Message;
            return payload;
        }

        if (action == "vendor_codes_list")
        {
            return await VendorCodesAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        if (action == "vendor_code_save")
        {
            var storageId = ParseId(First(fields, "storage_id", "id"));
            var written = await prices.SaveVendorCodeAsync(
                storageId,
                First(fields, "vendor_code", "vendor_short", "short_name"),
                First(fields, "vendor_full", "name"),
                cancellationToken).ConfigureAwait(false);
            return new FlagBody(written.Succeeded, written.Message);
        }

        if (!fields.ContainsKey("price_file"))
        {
            return new FlagBody(false, MultivendorChooseFile);
        }

        return new FlagBody(false, MultivendorFileStaysClassic);
    }

    private static async Task<JsonObject> MinPriceAclPayloadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var restrict = true;
        var groups = new JsonArray();
        var users = new JsonArray();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `restrict_min`, `group_ids_json`, `user_ids_json` FROM `epc_mv_min_price_acl` WHERE `id` = 1 LIMIT 1";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                restrict = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) != 0;
                FillIds(groups, reader.IsDBNull(1) ? "[]" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture));
                FillIds(users, reader.IsDBNull(2) ? "[]" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            restrict = true;
            groups.Clear();
            users.Clear();
        }

        var listed = new JsonArray();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id`, `value` FROM `groups` WHERE IFNULL(`for_backend`,0) = 0 ORDER BY `value` ASC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                var label = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
                if (id > 0 && label.Trim().Length > 0)
                {
                    listed.Add(new JsonObject { ["id"] = id, ["value"] = label.Trim() });
                }
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            listed.Clear();
        }

        return new JsonObject
        {
            ["status"] = true,
            ["acl"] = new JsonObject
            {
                ["restrict"] = restrict,
                ["group_ids"] = groups,
                ["user_ids"] = users,
            },
            ["groups"] = listed,
        };
    }

    private static async Task<JsonObject> VendorCodesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var vendors = new JsonArray();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `id`, `name`, `short_name`, `hidden`, `connection_options`
                FROM `shop_storages`
                WHERE `interface_type` = 2
                ORDER BY TRIM(`short_name`) ASC, TRIM(`name`) ASC, `id` ASC
                LIMIT 2000
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var options = reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty;
                var priceId = 0;
                var multivendor = false;
                try
                {
                    var node = JsonNode.Parse(string.IsNullOrWhiteSpace(options) ? "{}" : options) as JsonObject;
                    if (node is not null)
                    {
                        if (int.TryParse(node["price_id"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                        {
                            priceId = parsed;
                        }

                        multivendor = NodePresent(node, "epc_mv_vendor_code") || NodePresent(node, "epc_typed_price_ids");
                    }
                }
                catch (JsonException)
                {
                    priceId = 0;
                }

                var code = reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty;
                vendors.Add(new JsonObject
                {
                    ["id"] = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["vendor_full"] = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["vendor_code"] = code,
                    ["vendor_short"] = code,
                    ["hidden"] = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    ["price_id"] = priceId,
                    ["is_multivendor"] = multivendor,
                });
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            vendors.Clear();
        }

        return new JsonObject
        {
            ["status"] = true,
            ["vendors"] = vendors,
            ["count"] = vendors.Count,
        };
    }

    private static void FillIds(JsonArray target, string? raw)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.TryGetInt32(out var id) && id > 0)
                {
                    target.Add(id);
                }
            }
        }
        catch (JsonException)
        {
            target.Clear();
        }
    }

    private static bool NodePresent(JsonObject node, string name)
    {
        if (!node.TryGetPropertyValue(name, out var value) || value is null)
        {
            return false;
        }

        var text = value.ToString();
        return text.Length > 0 && text != "0" && text != "false" && text != "[]" && text != "{}";
    }

    private static string First(IReadOnlyDictionary<string, string> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
