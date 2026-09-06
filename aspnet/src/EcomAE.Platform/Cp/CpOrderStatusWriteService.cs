using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>statuses.php</c> save_action twin for order and line-item status refs.</summary>
public interface ICpOrderStatusWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? ordersJson,
        string? itemsJson,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default);

    Task<CpOrderStatusReadResult> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record CpOrderStatusRow(
    long Id,
    string Caption,
    string Color,
    int ForCreated,
    int ForPaid,
    int ForFinish,
    int ForInverse,
    int SortOrder);

public sealed record CpItemStatusRow(
    long Id,
    string Caption,
    string Color,
    int ForCreated,
    int ForFinish,
    int CountFlag,
    int SortOrder);

public sealed record CpOrderStatusReadResult(
    IReadOnlyList<CpOrderStatusRow> Orders,
    IReadOnlyList<CpItemStatusRow> Items,
    string Source,
    string Message);

public sealed class CpOrderStatusWriteService : ICpOrderStatusWriteService
{
    private const int MaxRows = 80;
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpOrderStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? ordersJson,
        string? itemsJson,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default)
    {
        var orders = ParseOrderStatuses(ordersJson);
        if (orders.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", orders.Error);
        }

        var items = ParseItemStatuses(itemsJson);
        if (items.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", items.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = NormalizeLang(langCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var keepOrders = new List<long>();
            for (var i = 0; i < orders.Rows.Count; i++)
            {
                var row = orders.Rows[i];
                var nameKey = await RequireTranslationAsync(
                    connection, transaction, row.ValueLangStrId, row.Name, lang, domainPath, cancellationToken).ConfigureAwait(false);
                if (row.CreatedEarlier)
                {
                    var updated = await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            UPDATE `shop_orders_statuses_ref`
                            SET `name`=?, `color`=?, `for_created`=?, `for_paid`=?, `for_finish`=?, `for_inverse`=?, `order`=?,
                                `to_manager_email`=?, `to_manager_sms`=?, `to_customer_email`=?, `to_customer_sms`=?
                            WHERE `id`=?
                            """),
                        cancellationToken,
                        nameKey, row.Color, row.ForCreated, row.ForPaid, row.ForFinish, row.ForInverse, i,
                        row.ToManagerEmail, row.ToManagerSms, row.ToCustomerEmail, row.ToCustomerSms, row.Id).ConfigureAwait(false);
                    if (updated <= 0)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return ErpSimpleWriteResult.Fail("not_found", "Order status " + row.Id.ToString(CultureInfo.InvariantCulture) + " was not updated.");
                    }

                    keepOrders.Add(row.Id);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            INSERT INTO `shop_orders_statuses_ref`
                            (`name`,`color`,`for_created`,`for_paid`,`for_finish`,`for_inverse`,`order`,
                             `to_manager_email`,`to_manager_sms`,`to_customer_email`,`to_customer_sms`)
                            VALUES (?,?,?,?,?,?,?,?,?,?,?)
                            """),
                        cancellationToken,
                        nameKey, row.Color, row.ForCreated, row.ForPaid, row.ForFinish, row.ForInverse, i,
                        row.ToManagerEmail, row.ToManagerSms, row.ToCustomerEmail, row.ToCustomerSms).ConfigureAwait(false);
                    keepOrders.Add(await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
                }
            }

            var keepItems = new List<long>();
            for (var i = 0; i < items.Rows.Count; i++)
            {
                var row = items.Rows[i];
                var nameKey = await RequireTranslationAsync(
                    connection, transaction, row.ValueLangStrId, row.Name, lang, domainPath, cancellationToken).ConfigureAwait(false);
                if (row.CreatedEarlier)
                {
                    var updated = await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            UPDATE `shop_orders_items_statuses_ref`
                            SET `name`=?, `color`=?, `for_created`=?, `for_finish`=?, `count_flag`=?, `issue_flag`=?, `order`=?,
                                `to_manager_email`=?, `to_manager_sms`=?, `to_customer_email`=?, `to_customer_sms`=?,
                                `for_return`=?, `check_for_return`=?, `complete_return`=?, `reject_return`=?
                            WHERE `id`=?
                            """),
                        cancellationToken,
                        nameKey, row.Color, row.ForCreated, row.ForFinish, row.CountFlag, row.IssueFlag, i,
                        row.ToManagerEmail, row.ToManagerSms, row.ToCustomerEmail, row.ToCustomerSms,
                        row.ForReturn, row.CheckForReturn, row.CompleteReturn, row.RejectReturn, row.Id).ConfigureAwait(false);
                    if (updated <= 0)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return ErpSimpleWriteResult.Fail("not_found", "Item status " + row.Id.ToString(CultureInfo.InvariantCulture) + " was not updated.");
                    }

                    keepItems.Add(row.Id);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            INSERT INTO `shop_orders_items_statuses_ref`
                            (`name`,`color`,`for_created`,`for_finish`,`count_flag`,`issue_flag`,`order`,
                             `to_manager_email`,`to_manager_sms`,`to_customer_email`,`to_customer_sms`,
                             `for_return`,`check_for_return`,`complete_return`,`reject_return`)
                            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                            """),
                        cancellationToken,
                        nameKey, row.Color, row.ForCreated, row.ForFinish, row.CountFlag, row.IssueFlag, i,
                        row.ToManagerEmail, row.ToManagerSms, row.ToCustomerEmail, row.ToCustomerSms,
                        row.ForReturn, row.CheckForReturn, row.CompleteReturn, row.RejectReturn).ConfigureAwait(false);
                    keepItems.Add(await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
                }
            }

            await DeleteMissingAsync(connection, transaction, "shop_orders_statuses_ref", keepOrders, cancellationToken).ConfigureAwait(false);
            await DeleteMissingAsync(connection, transaction, "shop_orders_items_statuses_ref", keepItems, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Order statuses saved.", keepOrders[0], keepOrders.Count + keepItems.Count);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save order statuses.");
        }
    }

    public async Task<CpOrderStatusReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var orders = new List<CpOrderStatusRow>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT s.`id`, IFNULL(t.`value`, IFNULL(s.`name`,'')) AS caption, IFNULL(s.`color`,'') AS color,
                    IFNULL(s.`for_created`,0) AS for_created, IFNULL(s.`for_paid`,0) AS for_paid,
                    IFNULL(s.`for_finish`,0) AS for_finish, IFNULL(s.`for_inverse`,0) AS for_inverse,
                    IFNULL(s.`order`,0) AS sort_order
                    FROM `shop_orders_statuses_ref` s
                    LEFT JOIN `lang_text_strings_translation` t ON t.`str_key` = s.`name` AND t.`lang_code` = 'en'
                    ORDER BY s.`order` ASC, s.`id` ASC
                    LIMIT 200
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    orders.Add(new CpOrderStatusRow(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["color"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["for_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["for_paid"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["for_finish"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["for_inverse"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sort_order"], CultureInfo.InvariantCulture)));
                }
            }

            var items = new List<CpItemStatusRow>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT s.`id`, IFNULL(t.`value`, IFNULL(s.`name`,'')) AS caption, IFNULL(s.`color`,'') AS color,
                    IFNULL(s.`for_created`,0) AS for_created, IFNULL(s.`for_finish`,0) AS for_finish,
                    IFNULL(s.`count_flag`,0) AS count_flag, IFNULL(s.`order`,0) AS sort_order
                    FROM `shop_orders_items_statuses_ref` s
                    LEFT JOIN `lang_text_strings_translation` t ON t.`str_key` = s.`name` AND t.`lang_code` = 'en'
                    ORDER BY s.`order` ASC, s.`id` ASC
                    LIMIT 200
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    items.Add(new CpItemStatusRow(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["color"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["for_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["for_finish"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["count_flag"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sort_order"], CultureInfo.InvariantCulture)));
                }
            }

            return new(orders, items, "database", string.Empty);
        }
        catch (System.Data.Common.DbException ex)
        {
            return new([], [], "database-error", ex.Message);
        }
    }

    public static (IReadOnlyList<OrderStatusDraft> Rows, string? Error) ParseOrderStatuses(string? raw)
    {
        var parsed = ParseArray(raw, "orders_statuses");
        if (parsed.Error is not null)
        {
            return ([], parsed.Error);
        }

        var rows = new List<OrderStatusDraft>();
        foreach (var item in parsed.Items)
        {
            var created = ReadLong(item, "created_earlier", "from_server") == 1;
            var id = ReadLong(item, "id");
            if (created && id <= 0)
            {
                return ([], "An existing order status id is required.");
            }

            rows.Add(new OrderStatusDraft(
                id,
                created,
                NormalizeName(ReadString(item, "value", "name", "caption")),
                ReadString(item, "value_lang_str_id", "valueLangStrId", "name"),
                NormalizeColor(ReadString(item, "color")),
                Flag(item, "for_created", "forCreated"),
                Flag(item, "for_paid", "forPaid"),
                Flag(item, "for_finish", "forFinish"),
                Flag(item, "for_inverse", "forInverse"),
                Flag(item, "to_manager_email", "toManagerEmail"),
                Flag(item, "to_manager_sms", "toManagerSms"),
                Flag(item, "to_customer_email", "toCustomerEmail"),
                Flag(item, "to_customer_sms", "toCustomerSms")));
        }

        return rows.Count == 0
            ? ([], "At least one order status is required.")
            : rows.Count > MaxRows
                ? ([], "Too many order statuses.")
                : (rows, null);
    }

    public static (IReadOnlyList<ItemStatusDraft> Rows, string? Error) ParseItemStatuses(string? raw)
    {
        var parsed = ParseArray(raw, "orders_items_statuses");
        if (parsed.Error is not null)
        {
            return ([], parsed.Error);
        }

        var rows = new List<ItemStatusDraft>();
        foreach (var item in parsed.Items)
        {
            var created = ReadLong(item, "created_earlier", "from_server") == 1;
            var id = ReadLong(item, "id");
            if (created && id <= 0)
            {
                return ([], "An existing item status id is required.");
            }

            rows.Add(new ItemStatusDraft(
                id,
                created,
                NormalizeName(ReadString(item, "value", "name", "caption")),
                ReadString(item, "value_lang_str_id", "valueLangStrId", "name"),
                NormalizeColor(ReadString(item, "color")),
                Flag(item, "for_created", "forCreated"),
                Flag(item, "for_finish", "forFinish"),
                Flag(item, "count_flag", "countFlag"),
                Flag(item, "issue_flag", "issueFlag"),
                Flag(item, "to_manager_email", "toManagerEmail"),
                Flag(item, "to_manager_sms", "toManagerSms"),
                Flag(item, "to_customer_email", "toCustomerEmail"),
                Flag(item, "to_customer_sms", "toCustomerSms"),
                Flag(item, "for_return", "forReturn"),
                Flag(item, "check_for_return", "checkForReturn"),
                Flag(item, "complete_return", "completeReturn"),
                Flag(item, "reject_return", "rejectReturn")));
        }

        return rows.Count == 0
            ? ([], "At least one item status is required.")
            : rows.Count > MaxRows
                ? ([], "Too many item statuses.")
                : (rows, null);
    }

    public readonly record struct OrderStatusDraft(
        long Id,
        bool CreatedEarlier,
        string Name,
        string ValueLangStrId,
        string Color,
        int ForCreated,
        int ForPaid,
        int ForFinish,
        int ForInverse,
        int ToManagerEmail,
        int ToManagerSms,
        int ToCustomerEmail,
        int ToCustomerSms);

    public readonly record struct ItemStatusDraft(
        long Id,
        bool CreatedEarlier,
        string Name,
        string ValueLangStrId,
        string Color,
        int ForCreated,
        int ForFinish,
        int CountFlag,
        int IssueFlag,
        int ToManagerEmail,
        int ToManagerSms,
        int ToCustomerEmail,
        int ToCustomerSms,
        int ForReturn,
        int CheckForReturn,
        int CompleteReturn,
        int RejectReturn);

    public static string NormalizeName(string? raw)
    {
        var text = WebUtility.HtmlEncode((raw ?? string.Empty).Trim());
        return text.Length <= 255 ? text : text[..255];
    }

    public static string NormalizeColor(string? raw)
    {
        var text = WebUtility.HtmlEncode((raw ?? string.Empty).Trim());
        if (text.Length == 0)
        {
            return "#777777";
        }

        return text.Length <= 32 ? text : text[..32];
    }

    private static (IReadOnlyList<JsonElement> Items, string? Error) ParseArray(string? raw, string field)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], field + " is required.");
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                return ([root.Clone()], null);
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                return ([], field + " must be a JSON array.");
            }

            var items = new List<JsonElement>();
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    items.Add(item.Clone());
                }
            }

            return (items, null);
        }
        catch (JsonException)
        {
            return ([], field + " is not valid JSON.");
        }
    }

    private static async Task DeleteMissingAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string table,
        IReadOnlyList<long> keep,
        CancellationToken cancellationToken)
    {
        var placeholders = string.Join(",", keep.Select(_ => "?"));
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `" + table + "` WHERE `id` NOT IN (" + placeholders + ")"),
            cancellationToken,
            keep.Cast<object?>().ToArray()).ConfigureAwait(false);
    }

    private async Task<string> RequireTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
        string langCode,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        var existingKey = (langStrId ?? string.Empty).Trim();
        if (existingKey is "0")
        {
            existingKey = string.Empty;
        }

        var isCustom = 0L;
        var hasTranslation = 0L;
        if (existingKey.Length > 0)
        {
            isCustom = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `is_custom` FROM `lang_text_strings` WHERE `str_key` = ? LIMIT 1"),
                cancellationToken,
                existingKey).ConfigureAwait(false);
            if (isCustom == 0)
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                    cancellationToken,
                    existingKey).ConfigureAwait(false);
                if (found == 0)
                {
                    existingKey = string.Empty;
                }
            }

            if (existingKey.Length > 0)
            {
                hasTranslation = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?"),
                    cancellationToken,
                    existingKey,
                    langCode).ConfigureAwait(false);
            }
        }

        string key;
        if (existingKey.Length == 0 || isCustom == 0)
        {
            key = await AllocateStrKeyAsync(connection, transaction, domainPath, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `is_custom`, `str_key`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                "STATUSES EDITING",
                null,
                0,
                1,
                key).ConfigureAwait(false);
            hasTranslation = 0;
        }
        else
        {
            key = existingKey;
        }

        if (hasTranslation > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?"),
                cancellationToken,
                value,
                key,
                langCode).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?)"),
                cancellationToken,
                value,
                key,
                langCode).ConfigureAwait(false);
        }

        return key;
    }

    private async Task<string> AllocateStrKeyAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            _createdStrings++;
            var key = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                      + "_"
                      + _createdStrings.ToString(CultureInfo.InvariantCulture)
                      + "_"
                      + LegacyPasswordVerifier.Md5Hex(domainPath ?? string.Empty);
            var found = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                cancellationToken,
                key).ConfigureAwait(false);
            if (found == 0)
            {
                return key;
            }
        }

        throw new ErpWriteException("Could not allocate a status translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }

    private static JsonElement GetProperty(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var prop))
            {
                return prop;
            }
        }

        return default;
    }

    private static long ReadLong(JsonElement item, params string[] names)
    {
        var prop = GetProperty(item, names);
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
        {
            return n;
        }

        if (prop.ValueKind == JsonValueKind.True)
        {
            return 1;
        }

        return prop.ValueKind == JsonValueKind.String
            && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int Flag(JsonElement item, params string[] names)
        => ReadLong(item, names) == 1 ? 1 : 0;

    private static string ReadString(JsonElement item, params string[] names)
    {
        var prop = GetProperty(item, names);
        return prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : prop.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? prop.ToString()
                : string.Empty;
    }
}
