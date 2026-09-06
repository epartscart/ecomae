using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_vendor_approvals.php</c> twins for approve / suspend / reject.
/// Approve also twins <c>epc_vendor_approve_account</c> + warehouse provision.
/// Schema-ensure (create tables / create <c>EPC_VENDOR</c> group) stays PHP.
/// </summary>
public interface ICpVendorApprovalWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(
        long accountId,
        string? action,
        long approvedBy = 0,
        CancellationToken cancellationToken = default);
}

public sealed class CpVendorApprovalWriteService : ICpVendorApprovalWriteService
{
    public const string VendorGroupKey = "EPC_VENDOR";

    private static readonly Regex ExtraSpaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ShortBanned = new(@"[/#'""\\]+", RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpVendorApprovalWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long accountId,
        string? action,
        long approvedBy = 0,
        CancellationToken cancellationToken = default)
    {
        if (accountId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A vendor account id is required.");
        }

        var next = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (next is not ("approve" or "suspend" or "reject"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Approve, suspend, or reject is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (next == "approve")
        {
            return await ApproveAsync(connection, accountId, approvedBy, cancellationToken).ConfigureAwait(false);
        }

        var status = next == "suspend" ? "suspended" : "rejected";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_vendor_accounts` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            status, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), accountId);
        return ErpSimpleWriteResult.Ok("Updated", accountId);
    }

    /// <summary>PHP <c>epc_multivendor_sanitize_short</c>.</summary>
    public static string SanitizeShort(string? raw)
    {
        var text = CollapseSpaces(raw);
        text = ShortBanned.Replace(text, string.Empty).Trim();
        return ClipChars(text, 64);
    }

    /// <summary>PHP <c>epc_multivendor_sanitize_full</c>.</summary>
    public static string SanitizeFull(string? raw)
        => ClipChars(CollapseSpaces(raw), 255);

    /// <summary>PHP <c>epc_multivendor_list_base_name</c>.</summary>
    public static string ListBaseName(string? vendorShort, string? vendorFull)
    {
        var shortName = SanitizeShort(vendorShort);
        var fullName = SanitizeFull(vendorFull);
        if (shortName.Length == 0)
        {
            return fullName.Length > 0 ? fullName : "Vendor";
        }

        if (fullName.Length == 0 || string.Equals(fullName, shortName, StringComparison.OrdinalIgnoreCase))
        {
            return shortName;
        }

        return shortName + " · " + ClipChars(fullName, 90);
    }

    private static async Task<ErpSimpleWriteResult> ApproveAsync(
        DbConnection connection,
        long accountId,
        long approvedBy,
        CancellationToken cancellationToken)
    {
        await using var select = connection.CreateCommand();
        select.CommandText = ErpDb.Positional(
            "SELECT `id`, `user_id`, `vendor_full`, `vendor_short` FROM `epc_vendor_accounts` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(select, accountId);
        long userId;
        string vendorFull;
        string vendorShort;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Vendor account not found.");
            }

            userId = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            vendorFull = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
            vendorShort = reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString() ?? "";
        }

        long groupId;
        try
        {
            groupId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1"),
                cancellationToken,
                VendorGroupKey).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Vendor group EPC_VENDOR is missing.");
        }

        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Vendor group EPC_VENDOR is missing.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
            cancellationToken,
            userId, groupId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_vendor_accounts` SET `status` = 'approved', `approved_by` = ?, `approved_at` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            approvedBy < 0 ? 0 : approvedBy, now, now, accountId).ConfigureAwait(false);

        var writes = 2;
        try
        {
            var storageId = await ProvisionWarehouseAsync(
                connection,
                accountId,
                userId,
                vendorFull,
                vendorShort,
                cancellationToken).ConfigureAwait(false);
            if (storageId > 0)
            {
                writes++;
            }
        }
        catch (DbException)
        {
            // PHP still returns true when warehouse provision fails.
        }

        return new ErpSimpleWriteResult(true, "ok", "Approved", accountId, writes);
    }

    private static async Task<long> ProvisionWarehouseAsync(
        DbConnection connection,
        long accountId,
        long userId,
        string vendorFullRaw,
        string vendorShortRaw,
        CancellationToken cancellationToken)
    {
        var full = SanitizeFull(vendorFullRaw);
        var shortName = SanitizeShort(vendorShortRaw);
        if (shortName.Length == 0)
        {
            return 0;
        }

        if (full.Length == 0)
        {
            full = shortName;
        }

        var price = await ResolveOrCreatePriceListAsync(connection, shortName, cancellationToken).ConfigureAwait(false);
        if (price <= 0)
        {
            return 0;
        }

        var storageId = await EnsureWarehouseAsync(connection, full, shortName, price, cancellationToken).ConfigureAwait(false);
        if (storageId <= 0)
        {
            return 0;
        }

        await AttachUserAsync(connection, storageId, userId, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_vendor_accounts` SET `storage_id` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            storageId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), accountId).ConfigureAwait(false);
        return storageId;
    }

    private static async Task<long> ResolveOrCreatePriceListAsync(
        DbConnection connection,
        string listName,
        CancellationToken cancellationToken)
    {
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_prices` WHERE `name` = ? LIMIT 1"),
            cancellationToken,
            listName).ConfigureAwait(false);
        if (id > 0)
        {
            return id;
        }

        id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_prices` WHERE UPPER(`name`) = UPPER(?) LIMIT 1"),
            cancellationToken,
            listName).ConfigureAwait(false);
        if (id > 0)
        {
            return id;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `shop_docpart_prices`
                (`name`,`load_mode`,`strings_to_left`,`manufacturer_col`,`article_col`,`name_col`,`exist_col`,`price_col`,`time_to_exe_col`,`storage_col`,`min_order_col`,`clean_before`,`file_name_substring`,`encoding`,`separator`,`h_time`)
                VALUES (?, 1, 1, 1, 2, 3, 4, 5, 7, 0, 0, 1, ?, ?, ?, ?)
                """),
            cancellationToken,
            listName, listName, "utf-8", ",", "0").ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> EnsureWarehouseAsync(
        DbConnection connection,
        string vendorFull,
        string vendorShort,
        long priceId,
        CancellationToken cancellationToken)
    {
        var row = await FindWarehouseAsync(connection, vendorFull, vendorShort, cancellationToken).ConfigureAwait(false);
        var opts = BuildWarehouseOptions(row.OptionsJson, vendorFull, vendorShort, priceId, setPrimaryPriceId: true);
        if (row.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `shop_storages`
                    SET `name` = ?, `short_name` = ?, `interface_type` = 2, `connection_options` = ?, `hidden` = 0
                    WHERE `id` = ?
                    """),
                cancellationToken,
                vendorFull, vendorShort, opts, row.Id).ConfigureAwait(false);
            await LinkStorageNamesAsync(connection, vendorFull, vendorShort, priceId, cancellationToken).ConfigureAwait(false);
            await MapOfficeAsync(connection, row.Id, cancellationToken).ConfigureAwait(false);
            return row.Id;
        }

        var currency = await ReadShopCurrencyAsync(connection, cancellationToken).ConfigureAwait(false);
        var users = await FirstBackendUsersJsonAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `shop_storages`
                (`name`, `interface_type`, `users`, `connection_options`, `currency`, `short_name`, `hidden`, `bg_line_color`)
                VALUES (?, 2, ?, ?, ?, ?, 0, 0)
                """),
            cancellationToken,
            vendorFull, users, opts, currency, vendorShort).ConfigureAwait(false);
        var storageId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (storageId <= 0)
        {
            return 0;
        }

        await LinkStorageNamesAsync(connection, vendorFull, vendorShort, priceId, cancellationToken).ConfigureAwait(false);
        await MapOfficeAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
        return storageId;
    }

    private static async Task<(long Id, string? OptionsJson)> FindWarehouseAsync(
        DbConnection connection,
        string vendorFull,
        string vendorShort,
        CancellationToken cancellationToken)
    {
        await using (var primary = connection.CreateCommand())
        {
            primary.CommandText = ErpDb.Positional(
                """
                SELECT `id`, `connection_options`
                FROM `shop_storages`
                WHERE UPPER(TRIM(`name`)) = UPPER(?)
                  AND UPPER(TRIM(`short_name`)) = UPPER(?)
                LIMIT 1
                """);
            ErpDb.AddParameters(primary, vendorFull, vendorShort);
            await using var reader = await primary.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString());
            }
        }

        await using var legacy = connection.CreateCommand();
        legacy.CommandText = ErpDb.Positional(
            """
            SELECT `id`, `connection_options`
            FROM `shop_storages`
            WHERE UPPER(TRIM(`name`)) = UPPER(?)
              AND (TRIM(COALESCE(`short_name`, '')) = '' OR UPPER(TRIM(`short_name`)) = UPPER(?))
            LIMIT 1
            """);
        ErpDb.AddParameters(legacy, vendorFull, vendorShort);
        await using var fallback = await legacy.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await fallback.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (
                Convert.ToInt64(fallback.GetValue(0), CultureInfo.InvariantCulture),
                fallback.IsDBNull(1) ? null : fallback.GetValue(1)?.ToString());
        }

        return (0, null);
    }

    private static string BuildWarehouseOptions(
        string? existingJson,
        string vendorFull,
        string vendorShort,
        long priceId,
        bool setPrimaryPriceId)
    {
        var opts = new JsonObject
        {
            ["probability"] = "100",
            ["epc_mv_vendor_full"] = vendorFull,
            ["epc_mv_vendor_code"] = vendorShort,
        };
        if (setPrimaryPriceId)
        {
            opts["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                var parsed = JsonNode.Parse(existingJson);
                if (parsed is JsonObject existing)
                {
                    foreach (var prop in existing)
                    {
                        if (!opts.ContainsKey(prop.Key))
                        {
                            opts[prop.Key] = prop.Value?.DeepClone();
                        }
                    }

                    if (!setPrimaryPriceId
                        && existing["price_id"] is { } kept
                        && !string.IsNullOrWhiteSpace(kept.ToString()))
                    {
                        opts["price_id"] = kept.DeepClone();
                    }
                    else if (setPrimaryPriceId)
                    {
                        opts["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (JsonException)
            {
                // Fresh options when the stored JSON is junk.
            }
        }

        var typed = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (opts["epc_typed_price_ids"] is JsonArray existingTyped)
        {
            foreach (var item in existingTyped)
            {
                var key = (item?.ToString() ?? string.Empty).Trim();
                if (key.Length == 0 || !seen.Add(key))
                {
                    continue;
                }

                typed.Add(key);
            }
        }

        var priceKey = priceId.ToString(CultureInfo.InvariantCulture);
        if (seen.Add(priceKey))
        {
            typed.Add(priceKey);
        }

        opts["epc_typed_price_ids"] = typed;
        return opts.ToJsonString();
    }

    private static async Task LinkStorageNamesAsync(
        DbConnection connection,
        string vendorFull,
        string vendorShort,
        long priceId,
        CancellationToken cancellationToken)
    {
        var listBase = ListBaseName(vendorShort, vendorFull);
        await LinkStorageToListAsync(connection, listBase, priceId, cancellationToken).ConfigureAwait(false);
        await LinkStorageToListAsync(connection, vendorShort, priceId, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(vendorFull, vendorShort, StringComparison.OrdinalIgnoreCase))
        {
            await LinkStorageToListAsync(connection, vendorFull, priceId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task LinkStorageToListAsync(
        DbConnection connection,
        string storageName,
        long priceId,
        CancellationToken cancellationToken)
    {
        if (storageName.Length == 0 || priceId <= 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`, `connection_options` FROM `shop_storages` WHERE UPPER(`name`) = UPPER(?) LIMIT 1");
        ErpDb.AddParameters(command, storageName);
        long storageId;
        string? optionsJson;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            storageId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            optionsJson = reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString();
        }

        JsonObject opts;
        try
        {
            opts = string.IsNullOrWhiteSpace(optionsJson)
                ? new JsonObject()
                : JsonNode.Parse(optionsJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            opts = new JsonObject();
        }

        opts["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
        if (opts["probability"] is null || string.IsNullOrWhiteSpace(opts["probability"]?.ToString()))
        {
            opts["probability"] = "100";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_storages` SET `connection_options` = ? WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            opts.ToJsonString(), storageId).ConfigureAwait(false);
    }

    private static async Task MapOfficeAsync(
        DbConnection connection,
        long storageId,
        CancellationToken cancellationToken)
    {
        long officeId;
        try
        {
            officeId = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return;
        }

        if (officeId <= 0)
        {
            return;
        }

        try
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?"),
                cancellationToken,
                officeId, storageId).ConfigureAwait(false);
            if (exists > 0)
            {
                return;
            }
        }
        catch (DbException)
        {
            return;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `shop_offices_storages_map`
                    (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`)
                    VALUES (?, ?, 2, 0, 999999999, 0, 0)
                    """),
                cancellationToken,
                officeId, storageId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`) VALUES (?, ?)"),
                    cancellationToken,
                    officeId, storageId).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // PHP swallows office-map insert failures.
            }
        }
    }

    private static async Task AttachUserAsync(
        DbConnection connection,
        long storageId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 || storageId <= 0)
        {
            return;
        }

        string? raw;
        try
        {
            raw = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `users` FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                storageId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return;
        }

        var users = new JsonArray();
        var seen = false;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var parsed = JsonNode.Parse(raw);
                if (parsed is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        users.Add(item?.DeepClone());
                        var text = (item?.ToString() ?? string.Empty).Trim();
                        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var existing)
                            && existing == userId)
                        {
                            seen = true;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                users = new JsonArray();
            }
        }

        if (seen)
        {
            return;
        }

        users.Add(userId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_storages` SET `users` = ? WHERE `id` = ?"),
            cancellationToken,
            users.ToJsonString(), storageId).ConfigureAwait(false);
    }

    private static async Task<string> FirstBackendUsersJsonAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var admin = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `users` WHERE `user_type` = 2 ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (admin > 0)
            {
                return new JsonArray(admin.ToString(CultureInfo.InvariantCulture)).ToJsonString();
            }
        }
        catch (DbException)
        {
            // PHP falls back to [].
        }

        return "[]";
    }

    private static async Task<int> ReadShopCurrencyAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"),
                cancellationToken,
                "shop_currency").ConfigureAwait(false);
            if (int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n > 0)
            {
                return n;
            }
        }
        catch (DbException)
        {
            // PHP default 784 (AED numeric).
        }

        return 784;
    }

    private static string CollapseSpaces(string? raw)
        => ExtraSpaces.Replace((raw ?? string.Empty).Trim(), " ").Trim();

    private static string ClipChars(string value, int max)
        => value.Length <= max ? value : value[..max];
}
