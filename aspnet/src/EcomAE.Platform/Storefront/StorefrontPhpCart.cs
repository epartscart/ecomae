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
                    return await InsertType1Async(connection, product, userId, sessionId, techKey, cancellationToken).ConfigureAwait(false);
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

    /// <summary>PHP <c>ajax_change_count_need.php</c>. Type 1 reserves existing details, then other customer offices.</summary>
    public static async Task<object> ChangeCountAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        bool pricesVisible,
        string? requestObjectJson,
        bool techKeyPath,
        bool techKeyAccepted,
        CancellationToken cancellationToken,
        string? cityCookie = null)
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
                return await ChangeType1Async(connection, cartId, countNeed, userId, cityCookie, cancellationToken).ConfigureAwait(false);
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
                if (line is null)
                {
                    return new CartWriteBody(false, "sql_error", "SQL Error", null, null, null, null, null, null, null);
                }

                if (line.ProductType == 1)
                {
                    var released = await ReleaseType1Async(connection, id, cancellationToken).ConfigureAwait(false);
                    if (released is not null)
                    {
                        return released;
                    }

                    continue;
                }

                if (line.ProductType != 2)
                {
                    return new CartWriteBody(false, "sql_error", "SQL Error", null, null, null, null, null, null, null);
                }

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

    private static async Task<object> InsertType1Async(
        DbConnection connection,
        JsonElement product,
        int userId,
        int sessionId,
        string? techKey,
        CancellationToken cancellationToken)
    {
        var productId = JsonInt(product, "product_id");
        var officeId = JsonInt(product, "office_id");
        var storageId = JsonInt(product, "storage_id");
        var storageRecordText = JsonText(product, "storage_record_id");
        var priceText = JsonText(product, "price");
        var countNeed = JsonInt(product, "count_need");
        if (countNeed <= 0)
        {
            countNeed = 1;
        }

        var clientHash = JsonText(product, "check_hash").Trim();
        var computed = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            productId.ToString(CultureInfo.InvariantCulture)
            + officeId.ToString(CultureInfo.InvariantCulture)
            + storageId.ToString(CultureInfo.InvariantCulture)
            + storageRecordText
            + priceText
            + (techKey ?? string.Empty)))).ToLowerInvariant();
        if (!string.Equals(computed, clientHash, StringComparison.Ordinal))
        {
            return CartFail("35", CartStringHash);
        }

        if (!int.TryParse(storageRecordText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var storageRecordId))
        {
            storageRecordId = 0;
        }

        try
        {
            await using (var already = connection.CreateCommand())
            {
                already.CommandText = ErpDb.Positional(
                    "SELECT COUNT(*) FROM `shop_carts_details` WHERE `cart_record_id` IN (SELECT `id` FROM `shop_carts` WHERE `product_id` = ? AND `price` = ? AND `user_id` = ? AND `session_id` = ?) AND `storage_record_id` = ?");
                ErpDb.AddParameters(already, productId, priceText, userId, sessionId, storageRecordId);
                var count = Convert.ToInt32(await already.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture);
                if (count > 0)
                {
                    return new CartWriteBody(false, "already", null, null, null, null, null, null, null, null);
                }
            }

            var caption = await CatalogueCaptionAsync(connection, productId, cancellationToken).ConfigureAwait(false);
            if (caption is null)
            {
                return CartFail(null, CatalogueProductsMissing);
            }

            var (name, manufacturer, article, articleShow) = caption.Value;

            var groupId = 0;
            await using (var group = connection.CreateCommand())
            {
                group.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `id` LIMIT 1");
                ErpDb.AddParameters(group, userId);
                var scalar = await group.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (scalar is not null and not DBNull)
                {
                    groupId = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
                }
            }

            var markup = 0m;
            await using (var markupCommand = connection.CreateCommand())
            {
                markupCommand.CommandText = ErpDb.Positional(
                    "SELECT `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= ? AND `max_point` > ? LIMIT 1");
                ErpDb.AddParameters(markupCommand, officeId, storageId, groupId, priceText, priceText);
                var scalar = await markupCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (scalar is not null and not DBNull)
                {
                    markup = Convert.ToDecimal(scalar, CultureInfo.InvariantCulture);
                }
            }

            decimal priceNotMarkup = 0;
            decimal pricePurchase = 0;
            var exist = JsonInt(product, "exist");
            await using (var stock = connection.CreateCommand())
            {
                stock.CommandText = ErpDb.Positional(
                    """
                    SELECT `price` * (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`)) AS `price`,
                           `price` AS `price_not_markup`,
                           `price_purchase` * (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`)) AS `price_purchase`,
                           `exist`
                    FROM `shop_storages_data` WHERE `id` = ?
                    """);
                ErpDb.AddParameters(stock, storageRecordId);
                await using var reader = await stock.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return CartFail(null, WarehouseStockMissing);
                }

                priceNotMarkup = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                var purchased = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
                var sell = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);
                pricePurchase = purchased > 0 ? purchased : sell;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long cartId;
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = ErpDb.Positional(
                    """
                    INSERT INTO `shop_carts` (
                        `product_type`, `product_id`, `price`, `count_need`, `user_id`, `time`, `session_id`,
                        `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                        `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                        `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                        `t2_product_json`, `t2_json_params`
                    ) VALUES (1, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, '', ?, 1, 100, ?, 0, ?, ?, ?, '')
                    """);
                ErpDb.AddParameters(insert, productId, priceText, countNeed, userId, now, sessionId, manufacturer, article, articleShow, name, exist, JsonText(product, "time_to_exe"), storageId, markup, officeId, storageId, product.GetRawText());
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var idCommand = connection.CreateCommand())
            {
                idCommand.Transaction = transaction;
                idCommand.CommandText = "SELECT LAST_INSERT_ID()";
                cartId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            }

            await using (var reserve = connection.CreateCommand())
            {
                reserve.Transaction = transaction;
                reserve.CommandText = ErpDb.Positional("UPDATE `shop_storages_data` SET `exist` = (`exist` - ?), `reserved` = (`reserved` + ?) WHERE `id` = ?");
                ErpDb.AddParameters(reserve, countNeed, countNeed, storageRecordId);
                await reserve.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var detail = connection.CreateCommand())
            {
                detail.Transaction = transaction;
                detail.CommandText = ErpDb.Positional(
                    "INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price`, `price_purchase`) VALUES (?,?,?,?,?,?,?)");
                ErpDb.AddParameters(detail, cartId, officeId, storageId, storageRecordId, countNeed, priceNotMarkup, pricePurchase);
                await detail.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CartWriteBody(true, null, null, null, null, null, null, null, null, null);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return CartFail(null, Type1Missing(ex));
        }
    }

    private static async Task<object> ChangeType1Async(
        DbConnection connection,
        int cartId,
        int countNeed,
        int userId,
        string? cityCookie,
        CancellationToken cancellationToken)
    {
        int current;
        int productId;
        decimal price;
        await using (var cart = connection.CreateCommand())
        {
            cart.CommandText = ErpDb.Positional("SELECT `count_need`, `product_id`, `price` FROM `shop_carts` WHERE `id` = ?");
            ErpDb.AddParameters(cart, cartId);
            await using var reader = await cart.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return CartFail("cart_item_not_found", CartStringNotFound);
            }

            current = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            productId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            price = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
        }

        if (current == countNeed)
        {
            return CartFail("the_same_count", CartStringSameCount);
        }

        if (countNeed > current)
        {
            var left = countNeed - current;
            await using (var details = connection.CreateCommand())
            {
                details.CommandText = ErpDb.Positional("SELECT `id`, `storage_record_id`, `count_reserved` FROM `shop_carts_details` WHERE `cart_record_id` = ?");
                ErpDb.AddParameters(details, cartId);
                var rows = new List<(int Id, int StorageId, int Reserved)>();
                await using (var reader = await details.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add((
                            Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                            Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                            Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
                    }
                }

                foreach (var row in rows)
                {
                    if (left <= 0)
                    {
                        break;
                    }

                    int exist;
                    await using var stock = connection.CreateCommand();
                    stock.CommandText = ErpDb.Positional("SELECT `exist` FROM `shop_storages_data` WHERE `id` = ?");
                    ErpDb.AddParameters(stock, row.StorageId);
                    exist = Convert.ToInt32(await stock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture);
                    if (exist == 0)
                    {
                        continue;
                    }

                    var take = left <= exist ? left : exist;
                    await using (var reserve = connection.CreateCommand())
                    {
                        reserve.CommandText = ErpDb.Positional("UPDATE `shop_storages_data` SET `exist` = `exist` - ?, `reserved` = `reserved` + ? WHERE `id` = ?");
                        ErpDb.AddParameters(reserve, take, take, row.StorageId);
                        await reserve.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await using (var detail = connection.CreateCommand())
                    {
                        detail.CommandText = ErpDb.Positional("UPDATE `shop_carts_details` SET `count_reserved` = `count_reserved` + ? WHERE `id` = ?");
                        ErpDb.AddParameters(detail, take, row.Id);
                        await detail.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await UpdateCountByAsync(connection, cartId, take, cancellationToken).ConfigureAwait(false);
                    left -= take;
                    if (left == 0)
                    {
                        return new CartWriteBody(true, null, null, null, countNeed, cartId, null, null, null, null);
                    }
                }
            }

            if (left > 0)
            {
                try
                {
                    left = await ReserveOtherOfficesAsync(connection, cartId, productId, price, left, userId, cityCookie, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return CartFail(null, Type1Missing(ex));
                }

                if (left == 0)
                {
                    return new CartWriteBody(true, null, null, null, countNeed, cartId, null, null, null, null);
                }
            }

            int stored;
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = ErpDb.Positional("SELECT `count_need` FROM `shop_carts` WHERE `id` = ?");
                ErpDb.AddParameters(read, cartId);
                stored = Convert.ToInt32(await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? current, CultureInfo.InvariantCulture);
            }

            return new CartWriteBody(false, "not_enough", CartStringNotEnough, null, stored, cartId, null, null, null, null);
        }

        var minus = current - countNeed;
        await using (var details = connection.CreateCommand())
        {
            details.CommandText = ErpDb.Positional("SELECT `id`, `storage_record_id`, `count_reserved` FROM `shop_carts_details` WHERE `cart_record_id` = ? ORDER BY `id` DESC");
            ErpDb.AddParameters(details, cartId);
            var rows = new List<(int Id, int StorageId, int Reserved)>();
            await using (var reader = await details.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((
                        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
                }
            }

            foreach (var row in rows)
            {
                var cancel = row.Reserved >= minus ? minus : row.Reserved;
                var deleteRow = row.Reserved == cancel;
                await using (var stock = connection.CreateCommand())
                {
                    stock.CommandText = ErpDb.Positional("UPDATE `shop_storages_data` SET `exist` = `exist` + ?, `reserved` = `reserved` - ? WHERE `id` = ?");
                    ErpDb.AddParameters(stock, cancel, cancel, row.StorageId);
                    await stock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                if (deleteRow)
                {
                    await using var remove = connection.CreateCommand();
                    remove.CommandText = ErpDb.Positional("DELETE FROM `shop_carts_details` WHERE `id` = ?");
                    ErpDb.AddParameters(remove, row.Id);
                    await remove.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await using var detail = connection.CreateCommand();
                    detail.CommandText = ErpDb.Positional("UPDATE `shop_carts_details` SET `count_reserved` = `count_reserved` - ? WHERE `id` = ?");
                    ErpDb.AddParameters(detail, cancel, row.Id);
                    await detail.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await UpdateCountByAsync(connection, cartId, -cancel, cancellationToken).ConfigureAwait(false);
                minus -= cancel;
                if (minus == 0)
                {
                    return new CartWriteBody(true, null, null, null, countNeed, cartId, null, null, null, null);
                }
            }
        }

        return new CartWriteBody(false, "not_enough", CartStringNotEnough, null, countNeed, cartId, null, null, null, null);
    }

    private static async Task<object?> ReleaseType1Async(DbConnection connection, int cartId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = new List<(int Id, int StorageId, int Reserved)>();
            await using (var details = connection.CreateCommand())
            {
                details.CommandText = ErpDb.Positional("SELECT `id`, `storage_record_id`, `count_reserved` FROM `shop_carts_details` WHERE `cart_record_id` = ?");
                ErpDb.AddParameters(details, cartId);
                await using var reader = await details.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((
                        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                        reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
                }
            }

            foreach (var row in rows)
            {
                await using (var stock = connection.CreateCommand())
                {
                    stock.CommandText = ErpDb.Positional("UPDATE `shop_storages_data` SET `exist` = `exist` + ?, `reserved` = `reserved` - ? WHERE `id` = ?");
                    ErpDb.AddParameters(stock, row.Reserved, row.Reserved, row.StorageId);
                    await stock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await using var remove = connection.CreateCommand();
                remove.CommandText = ErpDb.Positional("DELETE FROM `shop_carts_details` WHERE `id` = ?");
                ErpDb.AddParameters(remove, row.Id);
                await remove.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var delete = connection.CreateCommand();
            delete.CommandText = ErpDb.Positional("DELETE FROM `shop_carts` WHERE `id` = ?");
            ErpDb.AddParameters(delete, cartId);
            var deleted = await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return deleted == 1 ? null : new CartWriteBody(false, "sql_error", "SQL Error", null, null, null, null, null, null, null);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message.Contains("shop_storages_data", StringComparison.OrdinalIgnoreCase)
                ? WarehouseStockMissing
                : CartDetailsMissing;
            return CartFail(null, message);
        }
    }

    private static string Type1Missing(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("shop_properties_values_text", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_properties_values_list", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_line_lists_items", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_categories_properties_map", StringComparison.OrdinalIgnoreCase)
            || message.Contains("lang_text_strings", StringComparison.OrdinalIgnoreCase)
            || message.Contains("category_id", StringComparison.OrdinalIgnoreCase))
        {
            return CatalogueArticlePropertiesMissing;
        }

        if (message.Contains("shop_catalogue_products", StringComparison.OrdinalIgnoreCase))
        {
            return CatalogueProductsMissing;
        }

        if (message.Contains("users_groups_bind", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_offices_storages_map", StringComparison.OrdinalIgnoreCase)
            || message.Contains("additional_time", StringComparison.OrdinalIgnoreCase))
        {
            return "Office storage markups are not in this database.";
        }

        if (message.Contains("shop_geo", StringComparison.OrdinalIgnoreCase) || message.Contains("shop_offices", StringComparison.OrdinalIgnoreCase))
        {
            return "Customer offices are not in this database.";
        }

        if (message.Contains("shop_storages_data", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_currencies", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_storages", StringComparison.OrdinalIgnoreCase)
            || message.Contains("arrival_time", StringComparison.OrdinalIgnoreCase)
            || message.Contains("product_id", StringComparison.OrdinalIgnoreCase))
        {
            return WarehouseStockMissing;
        }

        if (message.Contains("shop_carts_details", StringComparison.OrdinalIgnoreCase))
        {
            return CartDetailsMissing;
        }

        return CartMissing;
    }

    /// <summary>PHP <c>changeCountType1</c> sections 2.1 (in stock) and 2.2 (expected).</summary>
    private static async Task<int> ReserveOtherOfficesAsync(
        DbConnection connection,
        int cartId,
        int productId,
        decimal price,
        int left,
        int userId,
        string? cityCookie,
        CancellationToken cancellationToken)
    {
        var offices = await CustomerOfficesAsync(connection, cityCookie, cancellationToken).ConfigureAwait(false);
        var groupId = await FirstGroupAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        left = await ReserveOfficeSuppliesAsync(connection, cartId, productId, price, left, offices, groupId, now, inStock: true, cancellationToken).ConfigureAwait(false);
        if (left == 0)
        {
            return 0;
        }

        return await ReserveOfficeSuppliesAsync(connection, cartId, productId, price, left, offices, groupId, now, inStock: false, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReserveOfficeSuppliesAsync(
        DbConnection connection,
        int cartId,
        int productId,
        decimal price,
        int left,
        IReadOnlyList<int> offices,
        int groupId,
        long now,
        bool inStock,
        CancellationToken cancellationToken)
    {
        foreach (var officeId in offices)
        {
            if (left <= 0)
            {
                break;
            }

            var storages = await OfficeStoragesAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
            foreach (var storage in storages)
            {
                if (left <= 0)
                {
                    break;
                }

                if (inStock && storage.AdditionalTime > 0)
                {
                    continue;
                }

                var supplies = await OfficeSuppliesAsync(connection, officeId, storage.StorageId, groupId, productId, price, now, inStock, cancellationToken).ConfigureAwait(false);
                foreach (var supply in supplies)
                {
                    if (left <= 0)
                    {
                        break;
                    }

                    var take = left <= supply.Exist ? left : supply.Exist;
                    if (take <= 0)
                    {
                        continue;
                    }

                    await using (var reserve = connection.CreateCommand())
                    {
                        reserve.CommandText = ErpDb.Positional("UPDATE `shop_storages_data` SET `exist` = `exist` - ?, `reserved` = `reserved` + ? WHERE `id` = ?");
                        ErpDb.AddParameters(reserve, take, take, supply.Id);
                        await reserve.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await using (var detail = connection.CreateCommand())
                    {
                        detail.CommandText = ErpDb.Positional(
                            "INSERT INTO `shop_carts_details` (`cart_record_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `price_purchase`) VALUES (?,?,?,?,?,?)");
                        ErpDb.AddParameters(detail, cartId, officeId, storage.StorageId, supply.Id, take, supply.PricePurchase);
                        await detail.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await UpdateCountByAsync(connection, cartId, take, cancellationToken).ConfigureAwait(false);
                    left -= take;
                }
            }
        }

        return left;
    }

    private static async Task<List<int>> CustomerOfficesAsync(DbConnection connection, string? cityCookie, CancellationToken cancellationToken)
    {
        object? geoId = null;
        if (!string.IsNullOrWhiteSpace(cityCookie) && int.TryParse(cityCookie, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cookieGeo))
        {
            geoId = cookieGeo;
        }
        else
        {
            await using var min = connection.CreateCommand();
            min.CommandText = "SELECT MIN(`id`) FROM `shop_geo`";
            var scalar = await min.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is not null and not DBNull)
            {
                geoId = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }
        }

        var offices = new List<int>();
        if (geoId is not null)
        {
            await using var mapped = connection.CreateCommand();
            mapped.CommandText = ErpDb.Positional("SELECT `office_id` FROM `shop_offices_geo_map` WHERE `geo_id` = ?");
            ErpDb.AddParameters(mapped, geoId);
            await using var reader = await mapped.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                offices.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        if (offices.Count > 0)
        {
            return offices;
        }

        await using var first = connection.CreateCommand();
        first.CommandText = "SELECT `id` FROM `shop_offices` ORDER BY `id` LIMIT 1";
        var office = await first.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (office is not null and not DBNull)
        {
            offices.Add(Convert.ToInt32(office, CultureInfo.InvariantCulture));
        }

        return offices;
    }

    private static async Task<int> FirstGroupAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        await using var group = connection.CreateCommand();
        group.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `id` LIMIT 1");
        ErpDb.AddParameters(group, userId);
        var scalar = await group.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<List<(int StorageId, int AdditionalTime)>> OfficeStoragesAsync(
        DbConnection connection,
        int officeId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int StorageId, int AdditionalTime)>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT DISTINCT `storage_id`, `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ?");
        ErpDb.AddParameters(command, officeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<List<(int Id, int Exist, decimal PricePurchase)>> OfficeSuppliesAsync(
        DbConnection connection,
        int officeId,
        int storageId,
        int groupId,
        int productId,
        decimal price,
        long now,
        bool inStock,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int Id, int Exist, decimal PricePurchase)>();
        await using var command = connection.CreateCommand();
        command.CommandText = inStock
            ? ErpDb.Positional(
                """
                SELECT * FROM (
                    SELECT `id`, `exist`, `price` AS `price_purchase`,
                        `price` + `price` * (SELECT `markup` / 100 FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= `shop_storages_data`.`price` AND `max_point` > `shop_storages_data`.`price`) AS `customer_price`
                    FROM `shop_storages_data`
                    WHERE `product_id` = ? AND `arrival_time` < ? AND `exist` > 0 AND `storage_id` = ?
                ) AS `storage_data` WHERE `customer_price` = ?
                """)
            : ErpDb.Positional(
                """
                SELECT * FROM (
                    SELECT `id`, `exist`, `price` AS `price_purchase`,
                        `price` + `price` * (SELECT `markup` / 100 FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= `shop_storages_data`.`price` AND `max_point` > `shop_storages_data`.`price`) AS `customer_price`
                    FROM `shop_storages_data`
                    WHERE `product_id` = ? AND `exist` > 0 AND `storage_id` = ?
                ) AS `storage_data` WHERE `customer_price` = ?
                """);
        if (inStock)
        {
            ErpDb.AddParameters(command, officeId, storageId, groupId, productId, now, storageId, price);
        }
        else
        {
            ErpDb.AddParameters(command, officeId, storageId, groupId, productId, storageId, price);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task UpdateCountByAsync(DbConnection connection, int cartId, int delta, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("UPDATE `shop_carts` SET `count_need` = `count_need` + ? WHERE `id` = ?");
        ErpDb.AddParameters(command, delta, cartId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
