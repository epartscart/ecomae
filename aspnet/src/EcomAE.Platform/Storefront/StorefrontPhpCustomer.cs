using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const int EvaluationPageLimit = 20;

    public static async Task<object> SetUserOptionAsync(
        DbConnection connection,
        int userId,
        int sessionId,
        string? key,
        string? value,
        CancellationToken cancellationToken)
    {
        var optionKey = key ?? string.Empty;
        try
        {
            if (!await OptionKeyAllowedAsync(connection, optionKey, cancellationToken).ConfigureAwait(false))
            {
                return new OptionBody(false, "Forbidden");
            }
        }
        catch (OptionSchemaException)
        {
            return new OptionBody(false, CatalogueCategoriesMissing);
        }

        try
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users_options` WHERE `user_id` = ? AND `session_id` = ? AND `data_key` = ?"),
                cancellationToken,
                userId,
                sessionId,
                optionKey).ConfigureAwait(false);
            if (existing == 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `users_options` (`user_id`, `session_id`, `data_key`, `data_value`) VALUES (?,?,?,?)"),
                    cancellationToken,
                    userId,
                    sessionId,
                    optionKey,
                    value ?? string.Empty).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `users_options` SET `data_value` = ? WHERE `user_id` = ? AND `session_id` = ? AND `data_key` = ?"),
                    cancellationToken,
                    value ?? string.Empty,
                    userId,
                    sessionId,
                    optionKey).ConfigureAwait(false);
            }

            return new OptionBody(true, "Ok");
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new OptionBody(false, UserOptionsMissing);
        }
    }

    public static async Task<object> AddEvaluationAsync(
        DbConnection connection,
        int userId,
        string? evaluationObject,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return new OptionBody(false, "4088");
        }

        if (!TryObject(evaluationObject, out var evaluation))
        {
            return new OptionBody(false, "4090");
        }

        var productId = JsonInt(evaluation, "product_id");
        try
        {
            var already = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_products_evaluations` WHERE `user_id` = ? AND `product_id` = ?"),
                cancellationToken,
                userId,
                productId).ConfigureAwait(false);
            if (already > 0)
            {
                return new OptionBody(false, "4089");
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_products_evaluations` (`product_id`, `mark`, `text_plus`, `text_minus`, `text`, `user_id`, `time`, `hide_user_data`) VALUES (?,?,?,?,?,?,?,?)"),
                cancellationToken,
                productId,
                JsonInt(evaluation, "mark"),
                HtmlEntities(JsonText(evaluation, "text_plus")),
                HtmlEntities(JsonText(evaluation, "text_minus")),
                HtmlEntities(JsonText(evaluation, "text")),
                userId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                JsonInt(evaluation, "hide_user_data")).ConfigureAwait(false);
            return new EvalSaved(true);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new OptionBody(false, ProductEvaluationsMissing);
        }
    }

    public static async Task<object> ProductEvaluationsAsync(
        DbConnection connection,
        string? evaluationQuery,
        CancellationToken cancellationToken)
    {
        if (!TryObject(evaluationQuery, out var query))
        {
            return new RawHttp(string.Empty, "application/json; charset=utf-8");
        }

        var sort = JsonText(query, "asc_desc");
        if (sort is not ("asc" or "desc"))
        {
            return new RawHttp(string.Empty, "application/json; charset=utf-8");
        }

        var productId = JsonInt(query, "product_id");
        var mark = JsonInt(query, "mark");
        var page = JsonInt(query, "page");
        if (page < 0)
        {
            page = 0;
        }

        var start = EvaluationPageLimit * page;
        var order = sort == "asc" ? "ASC" : "DESC";
        var sql = mark == 0
            ? "SELECT `id`, `product_id`, `mark`, `text_plus`, `text_minus`, `text`, `time`, `hide_user_data`, (SELECT `data_value` FROM `users_profiles` WHERE `user_id` = `shop_products_evaluations`.`user_id` AND `data_key` = 'name') AS `user_name`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ?) AS `count_total` FROM `shop_products_evaluations` WHERE `product_id` = ? ORDER BY `id` " + order + " LIMIT " + start.ToString(CultureInfo.InvariantCulture) + ", " + EvaluationPageLimit.ToString(CultureInfo.InvariantCulture)
            : "SELECT `id`, `product_id`, `mark`, `text_plus`, `text_minus`, `text`, `time`, `hide_user_data`, (SELECT `data_value` FROM `users_profiles` WHERE `user_id` = `shop_products_evaluations`.`user_id` AND `data_key` = 'name') AS `user_name`, (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ?) AS `count_total` FROM `shop_products_evaluations` WHERE `product_id` = ? AND `mark` = ? ORDER BY `id` " + order + " LIMIT " + start.ToString(CultureInfo.InvariantCulture) + ", " + EvaluationPageLimit.ToString(CultureInfo.InvariantCulture);
        try
        {
            var rows = new List<EvaluationRow>();
            var pages = 0;
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(sql);
            if (mark == 0)
            {
                ErpDb.AddParameters(command, productId, productId);
            }
            else
            {
                ErpDb.AddParameters(command, productId, productId, mark);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (pages == 0)
                {
                    var total = reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture);
                    pages = total / EvaluationPageLimit;
                    if (total % EvaluationPageLimit != 0)
                    {
                        pages++;
                    }
                }

                var hidden = !reader.IsDBNull(7) && Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) == 1;
                var unix = reader.IsDBNull(6) ? 0L : Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture);
                rows.Add(new EvaluationRow(
                    Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    TextOrEmpty(reader, 3),
                    TextOrEmpty(reader, 4),
                    TextOrEmpty(reader, 5),
                    FormatPhpHour(unix),
                    hidden ? "4091" : reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture)));
            }

            return new EvaluationList(true, rows, pages);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message;
            if (message.Contains("users_profiles", StringComparison.OrdinalIgnoreCase))
            {
                return new OptionBody(false, "User profiles are not in this database.");
            }

            return new OptionBody(false, ProductEvaluationsMissing);
        }
    }

    public static async Task<object> ProductMarkAsync(
        DbConnection connection,
        string? productIdText,
        CancellationToken cancellationToken)
    {
        var productId = int.TryParse(productIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT COUNT(`id`) AS `marks_count`, ROUND(SUM(`mark`)/COUNT(`id`)) AS `general_mark`,
                (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ? AND `mark` = 1) AS `mark_1_count`,
                (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ? AND `mark` = 2) AS `mark_2_count`,
                (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ? AND `mark` = 3) AS `mark_3_count`,
                (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ? AND `mark` = 4) AS `mark_4_count`,
                (SELECT COUNT(`id`) FROM `shop_products_evaluations` WHERE `product_id` = ? AND `mark` = 5) AS `mark_5_count`
                FROM `shop_products_evaluations` WHERE `product_id` = ?
                """);
            ErpDb.AddParameters(command, productId, productId, productId, productId, productId, productId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return new MarkBody(
                true,
                CountText(reader, 0),
                reader.IsDBNull(1) ? null : WholeText(reader.GetValue(1)),
                CountText(reader, 2),
                CountText(reader, 3),
                CountText(reader, 4),
                CountText(reader, 5),
                CountText(reader, 6));
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new OptionBody(false, ProductEvaluationsMissing);
        }
    }

    public static async Task<object> VinMessagesAsync(
        DbConnection connection,
        int userId,
        string? vinIdText,
        string? manager,
        string? adminSession,
        string? adminUserId,
        CancellationToken cancellationToken)
    {
        var vinId = int.TryParse(vinIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var gate = await VinAllowedAsync(connection, userId, vinId, manager, adminSession, adminUserId, cancellationToken).ConfigureAwait(false);
        if (gate is not null)
        {
            return gate;
        }

        try
        {
            var rows = new List<VinMessageRow>();
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `time`, `is_customer`, `text` FROM `users_vin_messages` WHERE `vin_id` = ?");
            ErpDb.AddParameters(command, vinId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var unix = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var customer = !reader.IsDBNull(1) && Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0;
                rows.Add(new VinMessageRow(FormatPhpClock(unix), customer, TextOrEmpty(reader, 2)));
            }

            return rows;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new OptionBody(false, VinMessagesMissing);
        }
    }

    public static async Task<object> VinSendMessageAsync(
        DbConnection connection,
        int userId,
        string? vinIdText,
        string? text,
        string? manager,
        string? adminSession,
        string? adminUserId,
        CancellationToken cancellationToken)
    {
        var vinId = int.TryParse(vinIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var gate = await VinAllowedAsync(connection, userId, vinId, manager, adminSession, adminUserId, cancellationToken).ConfigureAwait(false);
        if (gate is not null)
        {
            return gate;
        }

        var customer = string.IsNullOrEmpty(manager) ? 1 : 0;
        var body = HtmlEntities((text ?? string.Empty).Trim());
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users_vin_messages` (`vin_id`, `is_customer`, `text`, `time`) VALUES (?,?,?,?)"),
                cancellationToken,
                vinId,
                customer,
                body,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            var viewedColumn = customer == 1 ? "viewed" : "viewed_customer";
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users_vin` SET `" + viewedColumn + "` = 0 WHERE `id` = ?"),
                cancellationToken,
                vinId).ConfigureAwait(false);
            return new EvalSaved(true);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message.Contains("users_vin_messages", StringComparison.OrdinalIgnoreCase)
                ? VinMessagesMissing
                : VinRequestsMissing;
            return new OptionBody(false, message);
        }
    }

    private static async Task<object?> VinAllowedAsync(
        DbConnection connection,
        int userId,
        int vinId,
        string? manager,
        string? adminSession,
        string? adminUserId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(manager))
        {
            try
            {
                var count = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                    cancellationToken,
                    adminSession ?? string.Empty,
                    adminUserId ?? string.Empty).ConfigureAwait(false);
                if (count != 1)
                {
                    return new NumericDenied(false, "Forbidden", 501);
                }
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new OptionBody(false, AdminSessionsMissing);
            }

            return null;
        }

        if (userId <= 0)
        {
            return new NumericDenied(false, "Forbidden", 501);
        }

        try
        {
            var owned = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users_vin` WHERE `id` = ? AND `user_id` = ?"),
                cancellationToken,
                vinId,
                userId).ConfigureAwait(false);
            if (owned == 0)
            {
                return new NumericDenied(false, "Forbidden", 501);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new OptionBody(false, VinRequestsMissing);
        }

        return null;
    }

    private static async Task<bool> OptionKeyAllowedAsync(DbConnection connection, string key, CancellationToken cancellationToken)
    {
        if (key is "propucts_request_0" or "selected_manufacturer")
        {
            return true;
        }

        if (!key.StartsWith("propucts_request_", StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = key["propucts_request_".Length..];
        if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId))
        {
            return false;
        }

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `id` = ?"),
                cancellationToken,
                categoryId).ConfigureAwait(false);
            return count > 0;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            throw new OptionSchemaException();
        }
    }

    private static bool TryObject(string? json, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string FormatPhpHour(long unix)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        return local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + " " + local.Hour.ToString(CultureInfo.InvariantCulture) + ":" + local.ToString("mm", CultureInfo.InvariantCulture);
    }

    private static string FormatPhpClock(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    private static string CountText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "0" : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private static string WholeText(object value)
    {
        var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        return decimal.Truncate(number) == number
            ? decimal.Truncate(number).ToString("0", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
    }

    private static string TextOrEmpty(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed class OptionSchemaException : Exception;

    public sealed record VinConnectDenied(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("code")] string Code);

    public sealed record OptionBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message);

    public sealed record EvalSaved(
        [property: JsonPropertyName("status")] bool Status);

    public sealed record EvaluationList(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("evaluations")] IReadOnlyList<EvaluationRow> Evaluations,
        [property: JsonPropertyName("pages_total")] int PagesTotal);

    public sealed record EvaluationRow(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("product_id")] string ProductId,
        [property: JsonPropertyName("mark")] string Mark,
        [property: JsonPropertyName("text_plus")] string TextPlus,
        [property: JsonPropertyName("text_minus")] string TextMinus,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("user_name")] string? UserName);

    public sealed record MarkBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("marks_count")] string MarksCount,
        [property: JsonPropertyName("general_mark")] string? GeneralMark,
        [property: JsonPropertyName("mark_1_count")] string Mark1,
        [property: JsonPropertyName("mark_2_count")] string Mark2,
        [property: JsonPropertyName("mark_3_count")] string Mark3,
        [property: JsonPropertyName("mark_4_count")] string Mark4,
        [property: JsonPropertyName("mark_5_count")] string Mark5);

    public sealed record VinMessageRow(
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("is_customer")] bool IsCustomer,
        [property: JsonPropertyName("text")] string Text);
}
