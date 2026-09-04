using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP <c>ajax_checkout_create.php</c> for signed-in customers.
/// Guest cookie/session checkout stays PHP.
/// </summary>
public interface IStorefrontCheckoutWriteService
{
    Task<StorefrontCheckoutWriteResult> CreateAsync(
        int userId,
        StorefrontCheckoutWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontCheckoutWriteRequest(
    int HowGetMode,
    int OfficeId = 0,
    bool UsersAgreement = false,
    string? OrderMessage = null,
    string? BuyerPoNumber = null);

public sealed record StorefrontCheckoutWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    long OrderId,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        status = Ok,
        surface = "storefront",
        status_token = Status,
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = true,
        phpAuthoritative = false,
        validation_code = Code,
        would_write = Ok && Writes > 0,
        order_id = OrderId,
        message = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontCheckoutWriteService : IStorefrontCheckoutWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontCheckoutWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<StorefrontCheckoutWriteResult> CreateAsync(
        int userId,
        StorefrontCheckoutWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0)
        {
            return Fail("auth", "Please log in or register to continue.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Cart database is not configured.");
        }

        if (!request.UsersAgreement)
        {
            return Fail("agreement", "Accept the user agreement to place the order.");
        }

        if (request.HowGetMode <= 0)
        {
            return Fail("how_get_missing", "Choose how you want to receive the order.");
        }

        if (request.HowGetMode == 1 && request.OfficeId <= 0)
        {
            return Fail("office_required", "Choose a pickup office.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var trade = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = 'epc_trade_approval_status' LIMIT 1"),
            cancellationToken,
            userId);
        if (!string.IsNullOrWhiteSpace(trade)
            && !string.Equals(trade, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("trade_not_approved", "Checkout is available after a manager approves your trade profile.");
        }

        var modeOk = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_obtaining_modes` WHERE `id` = ?"),
            cancellationToken,
            request.HowGetMode);
        if (modeOk != 1)
        {
            return Fail("how_get_invalid", "Delivery / pickup mode is not valid.");
        }

        var office = request.OfficeId;
        var cartCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = 0 AND `checked_for_order` = 1"),
            cancellationToken,
            userId);
        if (cartCount <= 0)
        {
            return Fail("cart_empty", "No cart lines are checked for order.");
        }

        var orderStatus = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_created` = 1 LIMIT 1",
            cancellationToken);
        var itemStatus = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_created` = 1 LIMIT 1",
            cancellationToken);
        if (orderStatus <= 0 || itemStatus <= 0)
        {
            return Fail("status_missing", "Created-order status is not configured.");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var how = new Dictionary<string, object?>
            {
                ["mode"] = request.HowGetMode,
            };
            if (office > 0)
            {
                how["office_id"] = office;
            }

            var howJson = JsonSerializer.Serialize(how);
            var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("""
                    INSERT INTO `shop_orders`
                    (`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`)
                    VALUES (?, 0, ?, 0, ?, 0, ?, ?, '', '')
                    """),
                cancellationToken,
                userId, time, orderStatus, request.HowGetMode, howJson);

            var orderId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
            if (orderId <= 0)
            {
                throw new ErpWriteException("Could not create the order header.");
            }

            await using var cartCmd = connection.CreateCommand();
            cartCmd.Transaction = tx;
            cartCmd.CommandText = ErpDb.Positional("""
                SELECT `id`, `product_type`, `product_id`, `price`, `count_need`,
                       `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                       `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                       `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`,
                       `t2_storage_id`, `t2_product_json`, `t2_json_params`
                FROM `shop_carts`
                WHERE `user_id` = ? AND `session_id` = 0 AND `checked_for_order` = 1
                """);
            ErpDb.AddParameters(cartCmd, userId);
            await using var reader = await cartCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var lines = new List<CartLine>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new CartLine(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["product_type"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["product_id"] is DBNull ? 0 : reader["product_id"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["price"] is DBNull ? 0 : reader["price"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["count_need"] is DBNull ? 0 : reader["count_need"], CultureInfo.InvariantCulture),
                    Text(reader, "t2_manufacturer"),
                    Text(reader, "t2_article"),
                    Text(reader, "t2_article_show"),
                    Text(reader, "t2_name"),
                    Convert.ToDecimal(reader["t2_exist"] is DBNull ? 0 : reader["t2_exist"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["t2_time_to_exe"] is DBNull ? 0 : reader["t2_time_to_exe"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["t2_time_to_exe_guaranteed"] is DBNull ? 0 : reader["t2_time_to_exe_guaranteed"], CultureInfo.InvariantCulture),
                    Text(reader, "t2_storage"),
                    Convert.ToDecimal(reader["t2_min_order"] is DBNull ? 1 : reader["t2_min_order"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["t2_probability"] is DBNull ? 0 : reader["t2_probability"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["t2_markup"] is DBNull ? 0 : reader["t2_markup"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["t2_price_purchase"] is DBNull ? 0 : reader["t2_price_purchase"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["t2_office_id"] is DBNull ? 0 : reader["t2_office_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["t2_storage_id"] is DBNull ? 0 : reader["t2_storage_id"], CultureInfo.InvariantCulture),
                    Text(reader, "t2_product_json"),
                    Text(reader, "t2_json_params")));
            }

            await reader.DisposeAsync();
            if (lines.Count == 0)
            {
                throw new ErpWriteException("No cart lines are checked for order.");
            }

            var firstOffice = office;
            var writes = 1;
            foreach (var line in lines)
            {
                var purchase = line.ProductType == 2 ? line.PricePurchase : 0m;
                if (line.ProductType == 1)
                {
                    purchase = await ErpDb.DecimalAsync(
                        connection,
                        tx,
                        ErpDb.Positional("SELECT IFNULL(`price_purchase`, 0) FROM `shop_carts_details` WHERE `cart_record_id` = ? ORDER BY `id` ASC LIMIT 1"),
                        cancellationToken,
                        line.Id);
                }

                if (line.Price <= purchase)
                {
                    throw new ErpWriteException("Unable to place this order right now. Please refresh and try again.");
                }

                if (firstOffice <= 0 && line.OfficeId > 0)
                {
                    firstOffice = line.OfficeId;
                }

                var productJson = line.ProductType == 1 ? line.ProductJson : "";
                if (line.ProductType == 2)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        tx,
                        ErpDb.Positional("""
                            INSERT INTO `shop_orders_items`
                            (`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,
                             `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                             `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                             `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                             `t2_product_json`,
                             `sao_state`,
                             `sao_robot`,
                             `t2_json_params`)
                            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?, '',
                             IFNULL((SELECT `state_id` FROM `shop_sao_states_types_link` WHERE `is_start` = 1 AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?)), 0),
                             IFNULL((SELECT `action_id` FROM `shop_sao_states_types_actions_link` WHERE `is_start` = 1 AND `state_type_id` = (SELECT `id` FROM `shop_sao_states_types_link` WHERE `is_start` = 1 AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?))), 0),
                             ?)
                            """),
                        cancellationToken,
                        orderId, line.ProductType, line.Price, line.CountNeed, line.ProductId, itemStatus,
                        line.Manufacturer, line.Article, line.ArticleShow, line.Name, line.Exist,
                        line.TimeToExe, line.TimeToExeGuaranteed, line.Storage, line.MinOrder,
                        line.Probability, line.Markup, line.PricePurchase, line.OfficeId, line.StorageId,
                        line.StorageId, line.StorageId, line.JsonParams);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        tx,
                        ErpDb.Positional("""
                            INSERT INTO `shop_orders_items`
                            (`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,
                             `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                             `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                             `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                             `t2_product_json`, `sao_state`, `sao_robot`, `t2_json_params`)
                            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?, ?, '?', '?', ?)
                            """),
                        cancellationToken,
                        orderId, line.ProductType, line.Price, line.CountNeed, line.ProductId, itemStatus,
                        line.Manufacturer, line.Article, line.ArticleShow, line.Name, line.Exist,
                        line.TimeToExe, line.TimeToExeGuaranteed, line.Storage, line.MinOrder,
                        line.Probability, line.Markup, 0m, line.OfficeId, line.StorageId,
                        productJson, line.JsonParams);
                }

                var itemId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                writes++;

                if (line.ProductType == 1)
                {
                    await using var detCmd = connection.CreateCommand();
                    detCmd.Transaction = tx;
                    detCmd.CommandText = ErpDb.Positional("SELECT `id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved` FROM `shop_carts_details` WHERE `cart_record_id` = ?");
                    ErpDb.AddParameters(detCmd, line.Id);
                    await using var detReader = await detCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    var details = new List<(long Id, int OfficeId, int StorageId, long StorageRecordId, decimal Reserved)>();
                    while (await detReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        details.Add((
                            Convert.ToInt64(detReader["id"], CultureInfo.InvariantCulture),
                            Convert.ToInt32(detReader["office_id"] is DBNull ? 0 : detReader["office_id"], CultureInfo.InvariantCulture),
                            Convert.ToInt32(detReader["storage_id"] is DBNull ? 0 : detReader["storage_id"], CultureInfo.InvariantCulture),
                            Convert.ToInt64(detReader["storage_record_id"] is DBNull ? 0 : detReader["storage_record_id"], CultureInfo.InvariantCulture),
                            Convert.ToDecimal(detReader["count_reserved"] is DBNull ? 0 : detReader["count_reserved"], CultureInfo.InvariantCulture)));
                    }

                    await detReader.DisposeAsync();
                    foreach (var d in details)
                    {
                        if (firstOffice <= 0 && d.OfficeId > 0)
                        {
                            firstOffice = d.OfficeId;
                        }

                        var pricePurchase = await ErpDb.DecimalAsync(
                            connection,
                            tx,
                            ErpDb.Positional("""
                                SELECT IFNULL(NULLIF(`price_purchase`,0), `price`)
                                 * IFNULL((SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`)), 1)
                                FROM `shop_storages_data` WHERE `id` = ? LIMIT 1
                                """),
                            cancellationToken,
                            d.StorageRecordId);

                        await ErpDb.ExecuteAsync(
                            connection,
                            tx,
                            ErpDb.Positional("""
                                INSERT INTO `shop_orders_items_details`
                                (`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`)
                                VALUES (?,?,?,?,?,?,0,0,?)
                                """),
                            cancellationToken,
                            orderId, itemId, d.OfficeId, d.StorageId, d.StorageRecordId, d.Reserved, pricePurchase);
                        writes++;
                        await ErpDb.ExecuteAsync(
                            connection,
                            tx,
                            ErpDb.Positional("DELETE FROM `shop_carts_details` WHERE `id` = ?"),
                            cancellationToken,
                            d.Id);
                    }
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("DELETE FROM `shop_carts` WHERE `id` = ? AND `user_id` = ? AND `checked_for_order` = 1"),
                    cancellationToken,
                    line.Id, userId);
            }

            if (firstOffice <= 0)
            {
                firstOffice = office;
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `shop_orders` SET `successfully_created` = 1, `office_id` = ? WHERE `id` = ?"),
                cancellationToken,
                firstOffice, orderId);

            var po = (request.BuyerPoNumber ?? string.Empty).Trim();
            if (po.Length > 64)
            {
                po = po[..64];
            }

            if (po.Length > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`) VALUES (?, 1, ?, ?)"),
                    cancellationToken,
                    orderId, "Purchase Order: " + po, time);
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,0,?,0)"),
                    cancellationToken,
                    orderId, time, userId, "Buyer PO: " + po);
            }

            var note = (request.OrderMessage ?? string.Empty).Trim()
                .Replace("\r", "", StringComparison.Ordinal)
                .Replace("\t", "", StringComparison.Ordinal)
                .Replace("\n", "<br/>", StringComparison.Ordinal);
            if (note.Length > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`) VALUES (?, 1, ?, ?)"),
                    cancellationToken,
                    orderId, note, time);
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?,?,?,0,'Order created')"),
                cancellationToken,
                orderId, time, userId);

            var garageId = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `active` = 1 LIMIT 1"),
                cancellationToken,
                userId);
            if (garageId > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_docpart_garage_orders` (`garage_id`, `order_id`) VALUES (?, ?)"),
                    cancellationToken,
                    garageId, orderId);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(
                true,
                "written",
                "ok",
                "Order #" + orderId.ToString(CultureInfo.InvariantCulture) + " created. Staff email notify remains PHP until the notify helper is ported.",
                orderId,
                writes);
        }
        catch (Exception ex) when (ex is ErpWriteException or DbException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Fail("checkout_failed", ex.Message);
        }
    }

    private static StorefrontCheckoutWriteResult Fail(string code, string message)
        => new(false, "error", code, message, 0, 0);

    private static string Text(DbDataReader reader, string name)
        => reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name], CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record CartLine(
        long Id,
        int ProductType,
        int ProductId,
        decimal Price,
        decimal CountNeed,
        string Manufacturer,
        string Article,
        string ArticleShow,
        string Name,
        decimal Exist,
        int TimeToExe,
        int TimeToExeGuaranteed,
        string Storage,
        decimal MinOrder,
        int Probability,
        decimal Markup,
        decimal PricePurchase,
        int OfficeId,
        int StorageId,
        string ProductJson,
        string JsonParams);
}
