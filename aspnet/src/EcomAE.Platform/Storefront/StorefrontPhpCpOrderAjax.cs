using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public static Task<object> AddOrderLogAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string orderId,
        string text,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodedBody(false, ReturnsForbidden, 501),
            async adminId =>
            {
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?,?,?,?,?)"),
                        cancellationToken,
                        orderId,
                        UnixNow(),
                        adminId,
                        1,
                        HtmlCompat(text)).ConfigureAwait(false);
                    return new CpCodedBody(true, "Ok", 0);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, OrderLogsMissing);
                }
                catch (DbException)
                {
                    return new CpCodedBody(false, SqlError, 401);
                }
            },
            cancellationToken);

    public static Task<object> SetOrdersViewedAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string requestObject,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodedBody(false, ReturnsForbidden, 501),
            async _ =>
            {
                if (!TryReadObject(requestObject, out var root) || !TryIdList(root, "orders", out var orders))
                {
                    return new FlagBody(false, SqlErrorLower);
                }

                var flag = root.TryGetProperty("viewed_flag", out var viewed) ? JsonText(viewed) : string.Empty;
                if (orders.Count == 0)
                {
                    return new FlagBody(false, SqlErrorLower);
                }

                var placeholders = string.Join(",", orders.Select(_ => "?"));
                var sql = "UPDATE `shop_orders_viewed` SET `viewed_flag` = ? WHERE `order_id` IN (" + placeholders + ");";
                var args = new object?[orders.Count + 1];
                args[0] = flag;
                for (var i = 0; i < orders.Count; i++)
                {
                    args[i + 1] = orders[i];
                }

                try
                {
                    await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
                    return new FlagBody(true, sql);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, OrderViewedMissing);
                }
                catch (DbException)
                {
                    return new FlagBody(false, SqlErrorLower);
                }
            },
            cancellationToken);

    public static Task<object> OrdersInfoAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string requestObject,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodedBody(false, ReturnsForbidden, 501),
            async _ =>
            {
                var userId = TryReadObject(requestObject, out var root) && root.TryGetProperty("user_id", out var id)
                    ? JsonText(id)
                    : string.Empty;
                try
                {
                    var count = await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(`id`) FROM `shop_orders_viewed` WHERE `user_id` = ? AND `viewed_flag` = 0"),
                        cancellationToken,
                        userId).ConfigureAwait(false);
                    return new FlagBody(true, count ?? "0");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, OrderViewedMissing);
                }
            },
            cancellationToken);

    public static Task<object> PaidOrdersCountAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodedBody(false, ReturnsForbidden, 501),
            async _ =>
            {
                try
                {
                    var count = await ErpDb.StringAsync(
                        connection,
                        null,
                        "SELECT COUNT(*) FROM `shop_orders` WHERE `status` IN (SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1)",
                        cancellationToken).ConfigureAwait(false);
                    return new CpCountBody(true, count ?? "0");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    var message = ex.Message.Contains("shop_orders_statuses_ref", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("for_paid", StringComparison.OrdinalIgnoreCase)
                        ? PaidStatusesMissing
                        : OrdersMissing;
                    return new FlagBody(false, message);
                }
            },
            cancellationToken);

    public static Task<object> DeleteUnpaidOrdersAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string ordersList,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new FlagBody(false, ReturnsForbidden),
            async _ =>
            {
                if (!TryIdListJson(ordersList, out var orders) || orders.Count == 0)
                {
                    return new FlagBody(false, "3482");
                }

                var placeholders = string.Join(",", orders.Select(_ => "?"));
                var inList = "(" + placeholders + ")";
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var paidArgs = new object?[orders.Count + 1];
                    paidArgs[0] = 0;
                    for (var i = 0; i < orders.Count; i++)
                    {
                        paidArgs[i + 1] = orders[i];
                    }

                    var paid = await ErpDb.LongAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `paid` != ? AND `id` IN " + inList),
                        cancellationToken,
                        paidArgs).ConfigureAwait(false);
                    if (paid > 0)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return new FlagBody(false, DeletePaidOrders);
                    }

                    await DeleteOrderRowsAsync(connection, transaction, "shop_orders", "id", inList, orders, OrdersMissing, cancellationToken).ConfigureAwait(false);
                    await DeleteOrderRowsAsync(connection, transaction, "shop_orders_items", "order_id", inList, orders, OrderItemsMissing, cancellationToken).ConfigureAwait(false);
                    await DeleteOrderRowsAsync(connection, transaction, "shop_orders_items_details", "order_id", inList, orders, OrderItemDetailsMissing, cancellationToken).ConfigureAwait(false);
                    await DeleteOrderRowsAsync(connection, transaction, "shop_orders_logs", "order_id", inList, orders, OrderLogsMissing, cancellationToken).ConfigureAwait(false);
                    await DeleteOrderRowsAsync(connection, transaction, "shop_orders_messages", "order_id", inList, orders, OrderMessagesMissing, cancellationToken).ConfigureAwait(false);
                    await DeleteOrderRowsAsync(connection, transaction, "shop_orders_viewed", "order_id", inList, orders, OrderViewedMissing, cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new CpStatusBody(true);
                }
                catch (OrderDeleteStop stop)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new FlagBody(false, stop.Message);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new FlagBody(false, ex.Message.Contains("paid", StringComparison.OrdinalIgnoreCase) ? OrderPaidMissing : OrdersMissing);
                }
            },
            cancellationToken);

    public static Task<object> SetUserCommentAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string userId,
        string comment,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodedBody(false, ReturnsForbidden, 501),
            async _ =>
            {
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `users` SET `comment` = ? WHERE `user_id` = ?"),
                        cancellationToken,
                        comment,
                        userId).ConfigureAwait(false);
                    return new CpCodedBody(true, "Ok", 0);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, ex.Message.Contains("comment", StringComparison.OrdinalIgnoreCase) ? UserCommentMissing : UserAccountsMissing);
                }
                catch (DbException)
                {
                    return new CpCodedBody(false, SqlError, 401);
                }
            },
            cancellationToken);

    public static Task<object> UsersAutocompleteAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string input,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new FlagBody(false, ReturnsForbidden),
            async _ =>
            {
                List<string> fields;
                try
                {
                    fields = await ProfileFieldsAsync(connection, cancellationToken).ConfigureAwait(false);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, RegistrationFieldsMissing);
                }

                var profileSql = string.Empty;
                foreach (var field in fields)
                {
                    if (profileSql.Length > 0)
                    {
                        profileSql += ",";
                    }

                    profileSql += " IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '" + field
                        + "' AND `user_id` = `users`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '"
                        + field + "' AND `user_id` = `users`.`user_id`)),'') ";
                }

                if (profileSql.Length > 0)
                {
                    profileSql = "," + profileSql;
                }

                var customer = " IF( `user_id` = 0, 'ID 0, " + GuestAutocomplete
                    + "', CONCAT( 'ID ', `user_id`, ', E-mail: ', IF(`email`!='', `email`, '" + PhoneNotSpecified
                    + "'), ', Телефон: ', IF(`phone`!='', `phone`, '" + PhoneNotSpecified + "') " + profileSql + " ) )";
                var sql = "SELECT `user_id`, " + customer + " AS `customer` FROM `users` WHERE " + customer + " LIKE ?";
                try
                {
                    var vars = new List<AutocompleteUser> { new(0, "ID 0, " + GuestAutocomplete) };
                    await using var command = connection.CreateCommand();
                    command.CommandText = ErpDb.Positional(sql);
                    ErpDb.AddParameters(command, "%" + input + "%");
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var id = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                        var info = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        vars.Add(new AutocompleteUser(id, info));
                    }

                    return new AutocompleteBody(true, string.Empty, vars);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, ex.Message.Contains("users_profiles", StringComparison.OrdinalIgnoreCase) ? UserProfilesMissing : UserAccountsMissing);
                }
            },
            cancellationToken);

    public static async Task<object> ReturnsInfoAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await IsAdminSessionAsync(connection, adminSession, adminUser, cancellationToken).ConfigureAwait(false))
            {
                return new ReturnsBadgeBody(0, 0);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, AdminSessionsMissing);
        }

        var closedId = await ClosedReturnStatusAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            var sql = "SELECT COUNT(*) FROM `shop_orders_returns` WHERE (`return_complete` IS NULL OR `return_complete` = 0)";
            object?[] args = [];
            if (closedId > 0)
            {
                sql += " AND `status_id` <> ?";
                args = [closedId];
            }

            var count = (int)await ErpDb.LongAsync(connection, null, args.Length == 0 ? sql : ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
            return new ReturnsBadgeBody(1, count);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, ReturnsHeaderMissing);
        }
    }

    public static Task<object> ProductAliasAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string productId,
        string categoryId,
        string alias,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new FlagBody(false, ReturnsForbidden),
            async _ =>
            {
                try
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `id` != ? AND `category_id` = ? AND `alias` = ?"),
                        cancellationToken,
                        productId,
                        categoryId,
                        alias).ConfigureAwait(false);
                    return new RawHttp(count > 0 ? "false" : "true", "text/html; charset=utf-8");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, CatalogueProductsMissing);
                }
            },
            cancellationToken);

    public static Task<object> SetVinViewedAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string requestObject,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodedBody(false, ReturnsForbidden, 501),
            async _ =>
            {
                if (!TryReadObject(requestObject, out var root) || !TryVinIds(root, out var ids))
                {
                    return new FlagBody(false, SqlErrorLower);
                }

                var flag = root.TryGetProperty("viewed_flag", out var viewed) && viewed.TryGetInt32(out var parsed)
                    ? parsed
                    : ParseId(viewed.ValueKind == JsonValueKind.Undefined ? "0" : JsonText(viewed));
                if (ids.Count == 0)
                {
                    return new FlagBody(false, SqlErrorLower);
                }

                var placeholders = string.Join(",", ids.Select(_ => "?"));
                var args = new object?[ids.Count + 1];
                args[0] = flag;
                for (var i = 0; i < ids.Count; i++)
                {
                    args[i + 1] = ids[i];
                }

                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `users_vin` SET `viewed` = ? WHERE `id` IN (" + placeholders + ")"),
                        cancellationToken,
                        args).ConfigureAwait(false);
                    return new FlagBody(true, string.Empty);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, VinRequestsMissing);
                }
                catch (DbException)
                {
                    return new FlagBody(false, SqlErrorLower);
                }
            },
            cancellationToken);

    private static async Task<object> WithCpAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        Func<object> forbidden,
        Func<int, Task<object>> body,
        CancellationToken cancellationToken)
    {
        if (postedCsrf is null)
        {
            return CsrfFailure("Error! CSRF 1");
        }

        if (postedCsrf.Length == 0)
        {
            return CsrfFailure("Error! CSRF 3");
        }

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                cancellationToken,
                adminSession ?? string.Empty,
                ParseId(adminUser)).ConfigureAwait(false);
            if (count == 0)
            {
                return CsrfFailure("Error! CSRF 3.1");
            }

            var stored = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                adminSession ?? string.Empty,
                ParseId(adminUser)).ConfigureAwait(false) ?? string.Empty;
            if (!string.Equals(stored, postedCsrf, StringComparison.Ordinal))
            {
                return CsrfFailure("Error! CSRF 4");
            }

            if (count != 1)
            {
                return new RawHttp(string.Empty, "text/html; charset=utf-8");
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, AdminSessionsMissing);
        }

        var adminId = ParseId(adminUser);
        if (adminId == 0)
        {
            return forbidden();
        }

        return await body(adminId).ConfigureAwait(false);
    }

    private static async Task DeleteOrderRowsAsync(
        DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string table,
        string column,
        string inList,
        IReadOnlyList<string> orders,
        string missing,
        CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `" + table + "` WHERE `" + column + "` IN " + inList),
                cancellationToken,
                orders.Cast<object?>().ToArray()).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            throw new OrderDeleteStop(missing);
        }
    }

    private static async Task<List<string>> ProfileFieldsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var fields = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            name = name.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("#", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal)
                .Replace("\"", string.Empty, StringComparison.Ordinal);
            if (Regex.IsMatch(name, "^[A-Za-z0-9_]+$"))
            {
                fields.Add(name);
            }
        }

        return fields;
    }

    private static async Task<int> ClosedReturnStatusAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var caption in new[] { "3798", "epc_ret_st_closed" })
            {
                var id = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1"),
                    cancellationToken,
                    caption).ConfigureAwait(false);
                if (id > 0)
                {
                    return (int)id;
                }
            }

            return (int)await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    private static bool TryReadObject(string raw, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryIdList(JsonElement root, string name, out List<string> ids)
    {
        ids = [];
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in value.EnumerateArray())
        {
            ids.Add(JsonText(item));
        }

        return true;
    }

    private static bool TryIdListJson(string raw, out List<string> ids)
    {
        ids = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                ids.Add(JsonText(item));
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryVinIds(JsonElement root, out List<string> ids)
    {
        ids = [];
        if (!root.TryGetProperty("vins", out var value))
        {
            return false;
        }

        IEnumerable<string> tokens = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Select(JsonText),
            JsonValueKind.String => (value.GetString() ?? string.Empty).Trim().Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => []
        };
        foreach (var token in tokens)
        {
            if (!Regex.IsMatch(token, "^[0-9]+$"))
            {
                ids = [];
                return false;
            }

            ids.Add(token);
        }

        return true;
    }

    private static string JsonText(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Null => string.Empty,
            _ => value.ToString()
        };

    public sealed record CpCodedBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("code")] int Code);

    public sealed record CpCountBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("count")] string Count);

    public sealed record CpStatusBody(
        [property: JsonPropertyName("status")] bool Status);

    public sealed record ReturnsBadgeBody(
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("message")] int Message);

    public sealed record AutocompleteBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("vars")] IReadOnlyList<AutocompleteUser> Vars);

    public sealed record AutocompleteUser(
        [property: JsonPropertyName("user_id")] int UserId,
        [property: JsonPropertyName("user_info")] string UserInfo);

    private sealed class OrderDeleteStop : Exception
    {
        public OrderDeleteStop(string message) : base(message)
        {
        }
    }
}
