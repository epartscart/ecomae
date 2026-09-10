using System.Data.Common;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_6_complete_session</c> last_updated / records_count twin,
/// <c>min_price_acl_save</c> / <c>epc_mv_min_price_acl_save</c> UPSERT,
/// <c>ajax_epc_storefront_storage_toggle</c> / <c>epc_ssf_set_toggle</c>,
/// and <c>vendor_code_save</c> / <c>epc_multivendor_vendor_code_save</c>.
/// CSV import, file ingest, and sitemap stay Classic. This service does not invent a send.
/// </summary>
public interface ICpPricesUploadWriteService
{
    Task<ErpSimpleWriteResult> CompleteSessionAsync(long priceId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveMinPriceAclAsync(
        string? restrictRaw,
        string? groupIds,
        string? userIds,
        long updatedBy,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetStorefrontToggleAsync(
        string? entityTypeRaw,
        long entityId,
        string? storefrontEnabledRaw,
        long userId,
        string? userLabel,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveVendorCodeAsync(
        long storageId,
        string? vendorCode,
        string? vendorFull,
        CancellationToken cancellationToken = default);
}

public sealed class CpPricesUploadWriteService : ICpPricesUploadWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPricesUploadWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CompleteSessionAsync(
        long priceId,
        CancellationToken cancellationToken = default)
    {
        if (priceId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A price list id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            stamp, priceId);

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?"),
                cancellationToken,
                priceId);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?"),
                cancellationToken,
                count, priceId);
        }
        catch (Exception)
        {
            // PHP ignores a missing records_count column.
        }

        return ErpSimpleWriteResult.Ok("Price list session completed.", priceId);
    }

    public async Task<ErpSimpleWriteResult> SaveMinPriceAclAsync(
        string? restrictRaw,
        string? groupIds,
        string? userIds,
        long updatedBy,
        CancellationToken cancellationToken = default)
    {
        var restrict = ParseRestrict(restrictRaw) ? 1 : 0;
        var groups = ParseIdList(groupIds);
        var users = ParseIdList(userIds);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_mv_min_price_acl`
                     (`id`,`restrict_min`,`group_ids_json`,`user_ids_json`,`updated_at`,`updated_by`)
                     VALUES (1,?,?,?,?,?)
                     ON DUPLICATE KEY UPDATE
                     `restrict_min`=VALUES(`restrict_min`),
                     `group_ids_json`=VALUES(`group_ids_json`),
                     `user_ids_json`=VALUES(`user_ids_json`),
                     `updated_at`=VALUES(`updated_at`),
                     `updated_by`=VALUES(`updated_by`)
                    """),
                cancellationToken,
                restrict,
                JsonSerializer.Serialize(groups),
                JsonSerializer.Serialize(users),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Math.Max(0, updatedBy));
            return ErpSimpleWriteResult.Ok("Minimum price access saved", 1);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Min-price ACL table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetStorefrontToggleAsync(
        string? entityTypeRaw,
        long entityId,
        string? storefrontEnabledRaw,
        long userId,
        string? userLabel,
        CancellationToken cancellationToken = default)
    {
        var entityType = ParseEntityType(entityTypeRaw);
        var disabled = ParseStorefrontEnabled(storefrontEnabledRaw) ? 0 : 1;
        if (entityId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid entity id");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var label = Clip(userLabel, 128);
        if (label.Length == 0)
        {
            label = userId > 0 ? "user#" + userId.ToString(CultureInfo.InvariantCulture) : "admin";
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var nameSql = entityType == "price_list"
                ? "SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1"
                : "SELECT `name` FROM `shop_storages` WHERE `id` = ? LIMIT 1";
            var name = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional(nameSql),
                cancellationToken,
                entityId) ?? string.Empty;
            if (name.Length == 0)
            {
                return ErpSimpleWriteResult.Fail(
                    "not_found",
                    entityType == "price_list" ? "Price list not found" : "Storage not found");
            }

            var updateSql = entityType == "price_list"
                ? "UPDATE `shop_docpart_prices` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1"
                : "UPDATE `shop_storages` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1";
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(updateSql),
                cancellationToken,
                disabled,
                entityId);

            try
            {
                if (entityType == "price_list")
                {
                    await SyncStoragesFromPriceAsync(connection, entityId, disabled, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SyncPriceFromStorageAsync(connection, entityId, disabled, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
                // Linked column missing — schema-ensure stays Classic.
            }

            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_storefront_storage_toggle_audit`
                         (`entity_type`, `entity_id`, `entity_name`, `storefront_disabled`, `user_id`, `user_label`, `created_at`)
                         VALUES (?, ?, ?, ?, ?, ?, NOW())
                        """),
                    cancellationToken,
                    entityType,
                    entityId,
                    Clip(name, 255),
                    disabled,
                    Math.Max(0, userId),
                    label);
            }
            catch (DbException)
            {
                // Audit table missing — schema-ensure stays Classic.
            }

            return ErpSimpleWriteResult.Ok("Storefront visibility saved.", entityId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Storefront toggle column is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveVendorCodeAsync(
        long storageId,
        string? vendorCode,
        string? vendorFull,
        CancellationToken cancellationToken = default)
    {
        if (storageId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid warehouse id");
        }

        var newCode = SanitizeShort(vendorCode);
        if (newCode.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Vendor code cannot be empty");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var select = connection.CreateCommand();
            select.CommandText = ErpDb.Positional(
                "SELECT `name`, `short_name`, `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(select, storageId);
            string oldName;
            string oldCode;
            string optionsRaw;
            await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Warehouse not found");
                }

                oldName = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                oldCode = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
                optionsRaw = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "";
            }

            var postedFull = SanitizeFull(vendorFull);
            var full = postedFull.Length > 0 ? postedFull : oldName;
            if (full.Length == 0)
            {
                full = newCode;
            }

            var clash = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    SELECT `id` FROM `shop_storages`
                     WHERE UPPER(TRIM(`name`)) = UPPER(?) AND UPPER(TRIM(`short_name`)) = UPPER(?) AND `id` <> ?
                     LIMIT 1
                    """),
                cancellationToken,
                full,
                newCode,
                storageId);
            if (clash > 0)
            {
                return ErpSimpleWriteResult.Fail("conflict", "Another warehouse already uses this vendor name + code");
            }

            var opts = ParseOptions(optionsRaw);
            opts["epc_mv_vendor_full"] = full;
            opts["epc_mv_vendor_code"] = newCode;
            var encoded = opts.ToJsonString(new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_storages` SET `name` = ?, `short_name` = ?, `connection_options` = ? WHERE `id` = ?"),
                cancellationToken,
                full,
                newCode,
                encoded,
                storageId);

            try
            {
                await RenameLinkedPriceListsAsync(connection, opts, oldCode, oldName, newCode, full, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DbException)
            {
                // Best-effort price-list rename — schema-ensure stays Classic.
            }

            return ErpSimpleWriteResult.Ok(
                "Vendor code updated — storefront shows the new code; CP still shows the vendor name",
                storageId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Warehouse table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP <c>epc_multivendor_sanitize_short</c>.</summary>
    public static string SanitizeShort(string? raw)
    {
        var text = Regex.Replace((raw ?? string.Empty).Trim(), @"\s+", " ");
        text = Regex.Replace(text, @"[/#'""\\]+", "");
        text = text.Trim();
        return text.Length <= 64 ? text : text[..64];
    }

    /// <summary>PHP <c>epc_multivendor_sanitize_full</c>.</summary>
    public static string SanitizeFull(string? raw)
    {
        var text = Regex.Replace((raw ?? string.Empty).Trim(), @"\s+", " ");
        return text.Length <= 255 ? text : text[..255];
    }

    /// <summary>PHP entity_type is price_list or storage (default).</summary>
    public static string ParseEntityType(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return key == "price_list" ? "price_list" : "storage";
    }

    /// <summary>PHP: enabled when the raw value is nonempty and not 0.</summary>
    public static bool ParseStorefrontEnabled(string? raw)
    {
        var key = (raw ?? string.Empty).Trim();
        return key.Length > 0 && key != "0";
    }

    /// <summary>PHP: restrict unless the raw value is 0 / false / no / off.</summary>
    public static bool ParseRestrict(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return key is not ("0" or "false" or "no" or "off");
    }

    /// <summary>PHP group_ids / user_ids JSON array or comma/space split; positive ints only.</summary>
    public static IReadOnlyList<long> ParseIdList(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        var parsed = new List<long>();
        if (text.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        AddId(parsed, item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n)
                            ? n
                            : long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromText)
                                ? fromText
                                : 0);
                    }

                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (var part in text.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                AddId(parsed, id);
            }
        }

        return parsed;
    }

    private static void AddId(List<long> parsed, long id)
    {
        if (id <= 0 || parsed.Contains(id))
        {
            return;
        }

        if (parsed.Count >= 80)
        {
            return;
        }

        parsed.Add(id);
    }

    private static async Task SyncPriceFromStorageAsync(
        DbConnection connection,
        long storageId,
        int disabled,
        CancellationToken cancellationToken)
    {
        var raw = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            storageId);
        var priceId = PriceIdFromConnectionOptions(raw);
        if (priceId <= 0)
        {
            return;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_prices` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            disabled,
            priceId);
    }

    private static async Task SyncStoragesFromPriceAsync(
        DbConnection connection,
        long priceId,
        int disabled,
        CancellationToken cancellationToken)
    {
        await using var select = connection.CreateCommand();
        select.CommandText = ErpDb.Positional(
            """
            SELECT `id` FROM `shop_storages`
             WHERE `connection_options` LIKE CONCAT('%"price_id":', ?, '%')
            """);
        ErpDb.AddParameters(select, priceId);
        var ids = new List<long>();
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
        }

        foreach (var id in ids)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_storages` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                disabled,
                id);
        }
    }

    private static long PriceIdFromConnectionOptions(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("price_id", out var prop))
            {
                return 0;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            return long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromText)
                ? fromText
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static JsonObject ParseOptions(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static async Task RenameLinkedPriceListsAsync(
        DbConnection connection,
        JsonObject opts,
        string oldCode,
        string oldName,
        string newCode,
        string newFull,
        CancellationToken cancellationToken)
    {
        if (oldCode.Length == 0)
        {
            return;
        }

        var priceIds = new List<long>();
        AddPriceId(priceIds, opts["price_id"]);
        if (opts["epc_typed_price_ids"] is JsonArray typed)
        {
            foreach (var item in typed)
            {
                AddPriceId(priceIds, item);
            }
        }

        if (priceIds.Count == 0)
        {
            return;
        }

        var newList = ListBaseName(newCode, newFull);
        var oldBase = ListBaseName(oldCode, oldName.Length > 0 ? oldName : oldCode);
        foreach (var pid in priceIds.Distinct())
        {
            var current = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                pid) ?? string.Empty;
            if (current.Length == 0)
            {
                continue;
            }

            foreach (var suf in new[] { "", " · Sales", " · Purchase" })
            {
                if (current == oldBase + suf || current == oldCode + suf)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `shop_docpart_prices` SET `name` = ? WHERE `id` = ?"),
                        cancellationToken,
                        newList + suf,
                        pid);
                    break;
                }
            }
        }
    }

    private static void AddPriceId(List<long> ids, JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return;
        }

        if (value.TryGetValue<long>(out var n) && n > 0 && !ids.Contains(n))
        {
            ids.Add(n);
            return;
        }

        if (value.TryGetValue<string>(out var text)
            && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromText)
            && fromText > 0
            && !ids.Contains(fromText))
        {
            ids.Add(fromText);
        }
    }

    /// <summary>PHP <c>epc_multivendor_list_base_name</c>.</summary>
    public static string ListBaseName(string vendorShort, string vendorFull)
    {
        var code = SanitizeShort(vendorShort);
        var full = SanitizeFull(vendorFull);
        if (code.Length == 0)
        {
            return full.Length > 0 ? full : "Vendor";
        }

        if (full.Length == 0 || string.Equals(full, code, StringComparison.OrdinalIgnoreCase))
        {
            return code;
        }

        var suffix = full.Length <= 90 ? full : full[..90];
        return code + " · " + suffix;
    }
}
