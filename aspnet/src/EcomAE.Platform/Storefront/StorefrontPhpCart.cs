using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public readonly record struct SessionLook(bool MissingTable, int UserId, int SessionRecordId, bool Found);

    public static async Task<SessionLook> ReadSessionAsync(
        DbConnection connection,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return new SessionLook(false, 0, 0, false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `user_id`, `id` FROM `sessions` WHERE `session` = ? LIMIT 1");
            ErpDb.AddParameters(command, sessionToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new SessionLook(false, 0, 0, false);
            }

            var userId = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            var sessionId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            return new SessionLook(false, userId, sessionId, true);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new SessionLook(true, 0, 0, false);
        }
    }

    public static bool TechKeyAccepted(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public static object GuestCommerceDenied()
        => new CartWriteBody(false, "auth", GuestAuthMessage, GuestLoginUrl, null, null, null, null, null, null);

    public static object ChangeCountNoDatabase()
        => new CartWriteBody(false, "no_db_connect", NoDbConnectChange, null, null, null, null, null, null, null);

    /// <summary>PHP <c>ajax_add_to_basket.php</c> type-2 JSON. Type 1 does not reserve warehouse stock.</summary>
    public static async Task<object> AddToBasketAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        bool pricesVisible,
        string? productObjectsJson,
        string? techKey,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 && !pricesVisible)
        {
            return GuestCommerceDenied();
        }

        var sessionId = 0;
        if (userId > 0)
        {
            sessionId = 0;
        }
        else
        {
            if (sessionRecordId <= 0)
            {
                return CartFail("incorrect_session", CartStringIncorrectSession);
            }

            sessionId = sessionRecordId;
        }

        if (string.IsNullOrWhiteSpace(productObjectsJson))
        {
            return CartFail("incorrect_data", CartStringIncorrectData);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(productObjectsJson);
        }
        catch (JsonException)
        {
            return CartFail("incorrect_data", CartStringIncorrectData);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return CartFail("incorrect_data", CartStringIncorrectData);
            }

            var noError = true;
            foreach (var product in document.RootElement.EnumerateArray())
            {
                var productType = JsonInt(product, "product_type");
                if (productType == 1)
                {
                    return CartFail(CatalogueReserveCode, CatalogueReserve);
                }

                if (productType != 2)
                {
                    return CartFail("unknown_product_type", CartStringUnknownType);
                }

                var outcome = await InsertType2Async(connection, product, userId, sessionId, techKey, cancellationToken).ConfigureAwait(false);
                if (outcome.Stop)
                {
                    return outcome.Body ?? new CartWriteBody(false, null, null, null, null, null, null, null, null, null);
                }

                if (!outcome.Inserted)
                {
                    noError = false;
                }
            }

            return noError
                ? new CartWriteBody(true, null, null, null, null, null, null, null, null, null)
                : new CartWriteBody(false, null, null, null, null, null, null, null, null, null);
        }
    }

    /// <summary>PHP <c>ajax_change_count_need.php</c> type-2 quantity JSON. Type 1 does not move warehouse stock.</summary>
    public static async Task<object> ChangeCountAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        bool pricesVisible,
        string? requestObjectJson,
        bool techKeyPath,
        bool techKeyAccepted,
        CancellationToken cancellationToken)
    {
        if (techKeyPath && !techKeyAccepted)
        {
            return CartFail("forbidden", CartStringForbidden);
        }

        using var document = ParseObject(requestObjectJson);
        var root = document?.RootElement;
        if (techKeyPath)
        {
            userId = root is null ? 0 : JsonInt(root.Value, "user_id");
            sessionRecordId = 0;
        }
        else if (userId <= 0 && !pricesVisible)
        {
            return GuestCommerceDenied();
        }
        else if (userId <= 0 && sessionRecordId <= 0)
        {
            return CartFail("incorrect_session", CartStringIncorrectSession);
        }

        var sessionId = userId > 0 ? 0 : sessionRecordId;
        var countNeed = root is null ? 0 : JsonInt(root.Value, "count_need");
        var cartId = root is null ? 0 : JsonInt(root.Value, "id");
        try
        {
            if (!await OwnsCartAsync(connection, cartId, sessionId, userId, cancellationToken).ConfigureAwait(false))
            {
                return CartFail("cart_item_not_found", CartStringNotFound);
            }

            var line = await ReadCountLineAsync(connection, cartId, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return CartFail("cart_item_not_found", CartStringNotFound);
            }

            if (line.ProductType == 1)
            {
                return CartFail(CatalogueReserveCode, CatalogueReserve);
            }

            if (line.ProductType != 2)
            {
                return new CartWriteBody(false, "sql_error", "SQL Error", null, null, null, null, null, null, null);
            }

            return await ChangeType2Async(connection, cartId, countNeed, line, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return CartFail(null, CartMissing);
        }
    }

    /// <summary>PHP <c>ajax_delete_cart_record.php</c>. Type 2 deletes <c>shop_carts</c> only.</summary>
    public static async Task<object> DeleteCartAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        string? requestObjectJson,
        bool techKeyPath,
        bool techKeyAccepted,
        CancellationToken cancellationToken)
    {
        if (techKeyPath && !techKeyAccepted)
        {
            return new CartWriteBody(false, "forbidden", "Forbidden", null, null, null, null, null, null, null);
        }

        using var document = ParseObject(requestObjectJson);
        if (document is null || !document.RootElement.TryGetProperty("records_to_del", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            return CartFail("incorrect_data", CartStringIncorrectData);
        }

        var ids = new List<int>();
        foreach (var item in records.EnumerateArray())
        {
            ids.Add(item.ValueKind == JsonValueKind.Number
                ? item.GetInt32()
                : int.TryParse(item.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0);
        }

        var sessionId = userId > 0 ? 0 : sessionRecordId;
        if (!techKeyAccepted)
        {
            if (userId <= 0 && sessionRecordId <= 0)
            {
                return CartFail("incorrect_session", CartStringIncorrectSession);
            }

            try
            {
                foreach (var id in ids)
                {
                    if (!await OwnsCartAsync(connection, id, sessionId, userId, cancellationToken).ConfigureAwait(false))
                    {
                        return new CartWriteBody(false, "alien_cart", "Alien cart", null, null, null, null, null, null, null);
                    }
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return CartFail(null, CartMissing);
            }
        }

        try
        {
            foreach (var id in ids)
            {
                var line = await ReadCountLineAsync(connection, id, cancellationToken).ConfigureAwait(false);
                if (line is null || line.ProductType == 1)
                {
                    return CartFail(CatalogueReserveCode, CatalogueReserve);
                }

                if (line.ProductType != 2)
                {
                    return new CartWriteBody(false, "sql_error", "SQL Error", null, null, null, null, null, null, null);
                }
            }

            foreach (var id in ids)
            {
                await using var delete = connection.CreateCommand();
                delete.CommandText = ErpDb.Positional("DELETE FROM `shop_carts` WHERE `id` = ?");
                ErpDb.AddParameters(delete, id);
                var rows = await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (rows != 1)
                {
                    return new CartWriteBody(false, "sql_error", "SQL Error", null, null, null, null, null, null, null);
                }
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return CartFail(null, CartMissing);
        }

        return new CartWriteBody(true, "ok", "Ok", null, null, null, ids, null, null, null);
    }

    /// <summary>Stop is a PHP <c>exit</c>. Inserted false with Stop false matches <c>$no_error = false</c>.</summary>
    private static async Task<CartInsert> InsertType2Async(
        DbConnection connection,
        JsonElement product,
        int userId,
        int sessionId,
        string? techKey,
        CancellationToken cancellationToken)
    {
        var manufacturer = JsonText(product, "manufacturer");
        var article = JsonText(product, "article");
        var articleShow = JsonText(product, "article_show");
        var name = JsonText(product, "name");
        var exist = JsonInt(product, "exist");
        var timeToExe = JsonText(product, "time_to_exe");
        var timeGuaranteed = JsonText(product, "time_to_exe_guaranteed");
        var storage = JsonText(product, "storage");
        var minOrder = JsonInt(product, "min_order");
        if (minOrder <= 0)
        {
            minOrder = 1;
        }

        var probability = JsonInt(product, "probability");
        var price = FormatMoney(product, "price");
        var purchase = FormatMoney(product, "price_purchase");
        var markup = FormatMarkup(product);
        var officeId = JsonInt(product, "office_id");
        var storageId = JsonInt(product, "storage_id");
        var jsonParams = JsonParamsText(product);
        if (!OfferAllowsCart(price, purchase, markup))
        {
            return CartInsert.Exit(CartFail("no_margin", NoMarginMessage));
        }

        var countNeed = JsonInt(product, "count_need");
        if (countNeed <= 0)
        {
            countNeed = Math.Max(1, minOrder);
        }

        if (countNeed < minOrder)
        {
            countNeed = minOrder;
        }

        var clientHash = JsonText(product, "check_hash").Trim();
        if (clientHash is "0" || clientHash.Equals("null", StringComparison.OrdinalIgnoreCase) || clientHash.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            clientHash = string.Empty;
        }

        if (clientHash.Length > 0)
        {
            var computed = Type2Hash(manufacturer, article, articleShow, name, exist.ToString(CultureInfo.InvariantCulture), price, timeToExe, timeGuaranteed, storage, minOrder.ToString(CultureInfo.InvariantCulture), probability.ToString(CultureInfo.InvariantCulture), officeId.ToString(CultureInfo.InvariantCulture), storageId.ToString(CultureInfo.InvariantCulture), purchase, markup, jsonParams, techKey);
            if (!string.Equals(computed, clientHash, StringComparison.Ordinal))
            {
                return CartInsert.Exit(CartFail("35.2", HashExpiredMessage));
            }
        }

        var used = UsedPart(jsonParams);
        try
        {
            if (!used)
            {
                await using var check = connection.CreateCommand();
                check.CommandText = ErpDb.Positional(
                    """
                    SELECT COUNT(*) FROM `shop_carts` WHERE
                        `product_type` = 2 AND `user_id` = ? AND `session_id` = ? AND
                        `t2_manufacturer` = ? AND `t2_article` = ? AND `t2_exist` = ? AND
                        `t2_time_to_exe` = ? AND `t2_time_to_exe_guaranteed` = ? AND
                        `t2_probability` = ? AND `t2_office_id` = ? AND `t2_storage_id` = ? AND
                        CAST(`price` AS DECIMAL(18,4)) = CAST(? AS DECIMAL(18,4))
                    """);
                ErpDb.AddParameters(check, userId, sessionId, manufacturer, article, exist, timeToExe, timeGuaranteed, probability, officeId, storageId, price);
                var already = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture);
                if (already > 0)
                {
                    return CartInsert.Exit(new CartWriteBody(false, "already", null, null, null, null, null, null, null, null));
                }
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await using var insert = connection.CreateCommand();
            insert.CommandText = ErpDb.Positional(
                """
                INSERT INTO `shop_carts` (
                    `product_type`, `price`, `count_need`, `time`, `user_id`, `session_id`,
                    `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                    `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                    `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                    `t2_product_json`, `t2_json_params`
                ) VALUES (2, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """);
            ErpDb.AddParameters(
                insert,
                price,
                countNeed,
                now,
                userId,
                sessionId,
                manufacturer,
                article,
                articleShow,
                name,
                exist,
                timeToExe,
                timeGuaranteed,
                storage,
                minOrder,
                probability,
                markup,
                purchase,
                officeId,
                storageId,
                product.GetRawText(),
                jsonParams);
            var rows = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows > 0 ? CartInsert.Ok() : CartInsert.Failed();
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return CartInsert.Exit(CartFail(null, CartMissing));
        }
        catch (Exception)
        {
            return CartInsert.Failed();
        }
    }

    private readonly record struct CartInsert(bool Stop, bool Inserted, object? Body)
    {
        public static CartInsert Ok() => new(false, true, null);

        public static CartInsert Failed() => new(false, false, null);

        public static CartInsert Exit(object body) => new(true, false, body);
    }

    private static async Task<object> ChangeType2Async(
        DbConnection connection,
        int cartId,
        int countNeed,
        CountLine line,
        CancellationToken cancellationToken)
    {
        var exist = line.Exist;
        var minOrder = line.MinOrder;
        if (minOrder <= 0)
        {
            return CartFail("error", CartStringMinOrder);
        }

        if (countNeed < exist)
        {
            var multiple = false;
            var step = minOrder;
            for (var i = minOrder; i <= exist; i += minOrder)
            {
                step = i;
                if (i == countNeed)
                {
                    multiple = true;
                    break;
                }

                step = i + minOrder;
            }

            if (!multiple)
            {
                await UpdateCountAsync(connection, cartId, minOrder, cancellationToken).ConfigureAwait(false);
                return new CartWriteBody(false, "error", CartStringMinOrder, null, minOrder, cartId, null, step, minOrder, exist);
            }
        }

        if (line.CountNeed == countNeed)
        {
            return CartFail("the_same_count", CartStringSameCount);
        }

        if (countNeed > line.CountNeed)
        {
            if (countNeed > exist)
            {
                return new CartWriteBody(false, "not_enough", CartStringNotEnough, null, line.CountNeed, cartId, null, null, null, null);
            }

            await UpdateCountAsync(connection, cartId, countNeed, cancellationToken).ConfigureAwait(false);
            return new CartWriteBody(true, null, null, null, countNeed, cartId, null, null, null, null);
        }

        if (minOrder > countNeed)
        {
            await UpdateCountAsync(connection, cartId, minOrder, cancellationToken).ConfigureAwait(false);
            return new CartWriteBody(false, "not_enough", CartStringMinOrder, null, minOrder, cartId, null, null, null, null);
        }

        await UpdateCountAsync(connection, cartId, countNeed, cancellationToken).ConfigureAwait(false);
        return new CartWriteBody(true, null, null, null, countNeed, cartId, null, null, null, null);
    }

    private static async Task UpdateCountAsync(DbConnection connection, int cartId, int countNeed, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("UPDATE `shop_carts` SET `count_need` = ? WHERE `id` = ?");
        ErpDb.AddParameters(command, countNeed, cartId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> OwnsCartAsync(
        DbConnection connection,
        int cartId,
        int sessionId,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT COUNT(*) FROM `shop_carts` WHERE `id` = ? AND `session_id` = ? AND `user_id` = ?");
        ErpDb.AddParameters(command, cartId, sessionId, userId);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture);
        return count == 1;
    }

    private static async Task<CountLine?> ReadCountLineAsync(
        DbConnection connection,
        int cartId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `product_type`, `count_need`, `t2_exist`, `t2_min_order` FROM `shop_carts` WHERE `id` = ?");
        ErpDb.AddParameters(command, cartId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CountLine(
            reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
            reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
            reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture));
    }

    private static bool OfferAllowsCart(string priceText, string purchaseText, string markupText)
    {
        var sell = decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ? price : 0m;
        var purchase = decimal.TryParse(purchaseText, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) ? cost : 0m;
        var markup = decimal.TryParse(markupText, NumberStyles.Number, CultureInfo.InvariantCulture, out var mark) ? mark : 0m;
        if (sell <= 0)
        {
            return false;
        }

        if (markup > 0)
        {
            return true;
        }

        if (purchase <= 0)
        {
            return true;
        }

        if (sell > purchase + 0.0001m)
        {
            return true;
        }

        return Math.Abs(sell - purchase) <= 0.0001m;
    }

    private static string Type2Hash(
        string manufacturer,
        string article,
        string articleShow,
        string name,
        string exist,
        string price,
        string timeToExe,
        string timeGuaranteed,
        string storage,
        string minOrder,
        string probability,
        string officeId,
        string storageId,
        string purchase,
        string markup,
        string jsonParams,
        string? techKey)
    {
        var raw = manufacturer + article + articleShow + name + exist + price + timeToExe + timeGuaranteed + storage
            + minOrder + probability + officeId + storageId + purchase + markup + jsonParams + "2" + (techKey ?? string.Empty);
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private static bool UsedPart(string jsonParams)
    {
        if (jsonParams.Length == 0)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonParams);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("used", out var used)
                && JsonIntElement(used) == 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string FormatMoney(JsonElement product, string name)
    {
        if (!product.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "0.00";
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number.ToString("0.00", CultureInfo.InvariantCulture);
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString("0.00", CultureInfo.InvariantCulture)
            : text;
    }

    private static string FormatMarkup(JsonElement product)
    {
        if (!product.TryGetProperty("markup", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "0";
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return decimal.Truncate(number).ToString(CultureInfo.InvariantCulture);
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? decimal.Truncate(parsed).ToString(CultureInfo.InvariantCulture)
            : text;
    }

    private static string JsonParamsText(JsonElement product)
    {
        if (!product.TryGetProperty("json_params", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
    }

    private static string JsonText(JsonElement product, string name)
    {
        if (!product.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static int JsonIntElement(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static CartWriteBody CartFail(string? code, string? message)
        => new(false, code, message, null, null, null, null, null, null, null);

    public static bool RequestHasTechKey(string? requestObjectJson, bool emptyCounts)
    {
        using var document = ParseObject(requestObjectJson);
        if (document is null || !document.RootElement.TryGetProperty("tech_key", out var key) || key.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (!emptyCounts)
        {
            return true;
        }

        var text = key.ValueKind == JsonValueKind.String ? key.GetString() : key.ToString();
        return !string.IsNullOrEmpty(text);
    }

    public static string? RequestTechKey(string? requestObjectJson)
    {
        using var document = ParseObject(requestObjectJson);
        if (document is null || !document.RootElement.TryGetProperty("tech_key", out var key) || key.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return key.ValueKind == JsonValueKind.String ? key.GetString() : key.ToString();
    }

    private sealed record CountLine(int ProductType, int CountNeed, int Exist, int MinOrder);

    public sealed record CartWriteBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("code")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Code,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message,
        [property: JsonPropertyName("login_url")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? LoginUrl,
        [property: JsonPropertyName("count_need")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? CountNeed,
        [property: JsonPropertyName("id")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? Id,
        [property: JsonPropertyName("records_to_del")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<int>? RecordsToDel,
        [property: JsonPropertyName("i")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? Step,
        [property: JsonPropertyName("t2_min_order")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? MinOrder,
        [property: JsonPropertyName("t2_exist")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? Exist);
}
