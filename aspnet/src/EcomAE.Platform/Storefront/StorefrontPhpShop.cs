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
    public const string NotepadMissing = "Garage notepad is not in this database.";
    public const string GarageOrdersMissing = "Garage orders are not in this database.";
    public const string CartCheckMissing = "Cart check flag is not in this database.";
    public const string PriceListsMissing = "Price lists are not in this database.";
    public const string CatalogueArticlePropertiesMissing = "Catalogue article properties are not in this database.";
    public const string PaymentSystemsMissing = "Payment systems are not in this database.";
    public const string CheckoutNoMarginMessage = "Unable to place this order right now. Please refresh the page, remove any unavailable items, and try again. If the problem continues, contact support.";
    public const string CheckoutFailPrefix = "4492. ";
    public const string OrderItemDetailsMissing = "Order item details are not in this database.";
    public const string TradePendingMessage = EpcCustomerTrade.PendingCheckoutMessage;
    public const string TradeRejectedMessage = EpcCustomerTrade.RejectedCheckoutMessage;

    public static async Task<object> AddToNotepadAsync(
        DbConnection connection,
        int userId,
        string? garageText,
        string? productJson,
        CancellationToken cancellationToken)
    {
        var status = false;
        var message = "2304";
        var garageId = int.TryParse(garageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGarage) ? parsedGarage : 0;
        using var document = ParseObject(productJson);
        var product = document?.RootElement;

        if (userId <= 0)
        {
            return new NotepadBody(false, "2063");
        }

        var allowed = true;
        if (garageId > 0)
        {
            try
            {
                var owned = await ScalarLongAsync(
                    connection,
                    "SELECT COUNT(*) FROM `shop_docpart_garage` WHERE `user_id` = ? AND `id` = ?",
                    cancellationToken,
                    userId,
                    garageId).ConfigureAwait(false);
                if (owned <= 0)
                {
                    message = "2064";
                    allowed = false;
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new NotepadBody(false, GarageMissing);
            }
        }

        if (!allowed)
        {
            return new NotepadBody(false, message);
        }

        var article = CleanText(product, "article");
        if (article.Length == 0)
        {
            return new NotepadBody(false, "2068");
        }

        var comment = "4225 " + DateTime.Now.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        try
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = ErpDb.Positional(
                "INSERT INTO `shop_docpart_garage_notepad` (`user_id`, `garage_id`, `brend`, `article`, `name`, `exist`, `price`, `comment`) VALUES (?,?,?,?,?,?,?,?)");
            ErpDb.AddParameters(
                insert,
                userId,
                garageId,
                CleanText(product, "manufacturer"),
                article,
                CleanText(product, "name"),
                JsonInt(product, "exist"),
                JsonDecimal(product, "price"),
                comment);
            var rows = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            status = rows == 1;
            message = status ? "2066" : "2067";
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new NotepadBody(false, NotepadMissing);
        }

        return new NotepadBody(status, message);
    }

    public static async Task<object> GarageCarsAsync(
        DbConnection connection,
        int userId,
        string? requestObjectJson,
        CancellationToken cancellationToken)
    {
        using var document = ParseObject(requestObjectJson);
        if (document is null)
        {
            return new GarageAnswer { Status = false };
        }

        var root = document.RootElement;
        var action = JsonText(root, "action");
        var carId = JsonInt(root, "car_id");
        if (!string.Equals(action, "search", StringComparison.Ordinal))
        {
            try
            {
                var owned = await ScalarLongAsync(
                    connection,
                    "SELECT COUNT(*) FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ?",
                    cancellationToken,
                    carId,
                    userId).ConfigureAwait(false);
                if (owned <= 0)
                {
                    return new GarageAnswer { Status = false, Message = "No Access" };
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new GarageAnswer { Status = false, Message = GarageMissing };
            }
        }

        try
        {
            switch (action)
            {
                case "search":
                    return await SearchCarsAsync(connection, userId, HtmlEntities(JsonText(root, "search_str").Trim()), cancellationToken).ConfigureAwait(false);
                case "check_car":
                    return await CheckCarAsync(connection, JsonInt(root, "order_id"), carId, cancellationToken).ConfigureAwait(false);
                case "active_car":
                    return await ActiveCarAsync(connection, JsonInt(root, "user_id"), carId, cancellationToken).ConfigureAwait(false);
                case "delete_car":
                    await using (var delete = connection.CreateCommand())
                    {
                        delete.CommandText = ErpDb.Positional("DELETE FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ?");
                        ErpDb.AddParameters(delete, carId, JsonInt(root, "user_id"));
                        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return new GarageAnswer { Status = true };
                case "get_table_cars":
                    return await CarsTableAsync(connection, JsonInt(root, "customer_id"), JsonInt(root, "order_id"), cancellationToken).ConfigureAwait(false);
                default:
                    return new GarageAnswer { Status = false };
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message.Contains("shop_docpart_garage_orders", StringComparison.OrdinalIgnoreCase)
                ? GarageOrdersMissing
                : ex.Message.Contains("shop_docpart_cars", StringComparison.OrdinalIgnoreCase)
                    ? "Car marks are not in this database."
                    : GarageMissing;
            return new GarageAnswer { Status = false, Message = message };
        }
    }

    public static async Task<object> CheckoutCreateAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        string? agreement,
        string? howGet,
        string? phone,
        string? email,
        CancellationToken cancellationToken,
        string? requestHost = null)
    {
        if (userId > 0)
        {
            var trade = await TradeBlockAsync(connection, userId, cancellationToken).ConfigureAwait(false);
            if (trade is not null)
            {
                return trade;
            }
        }
        else if (!await GuestOrdersAllowedAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new ShopStatus(false, "4470", null, null);
        }

        var sessionId = 0;
        if (userId <= 0)
        {
            if (sessionRecordId <= 0)
            {
                return new ShopStatus(false, CartStringIncorrectSession, "incorrect_session", null);
            }

            sessionId = sessionRecordId;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.Equals(agreement, "yes", StringComparison.Ordinal))
            {
                throw new ShopStop("4471");
            }

            var cartCount = await ScalarLongAsync(
                connection,
                transaction,
                "SELECT COUNT(*) FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ?",
                cancellationToken,
                userId,
                sessionId).ConfigureAwait(false);
            if (cartCount == 0)
            {
                throw new ShopStop("4472");
            }

            var createdStatus = await ScalarLongAsync(
                connection,
                transaction,
                "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_created` = 1 LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (createdStatus <= 0)
            {
                throw new ShopStop("4473");
            }

            if (string.IsNullOrWhiteSpace(howGet))
            {
                throw new ShopStop("4474");
            }

            if (!TryHowGet(UnescapeCookie(howGet), out var mode, out var officeId, out var howGetJson))
            {
                throw new ShopStop("4475");
            }

            var modeCount = await ScalarLongAsync(
                connection,
                transaction,
                "SELECT COUNT(*) FROM `shop_obtaining_modes` WHERE `id` = ?",
                cancellationToken,
                mode).ConfigureAwait(false);
            if (modeCount != 1)
            {
                throw new ShopStop("4475");
            }

            if (mode == 1 && officeId <= 0)
            {
                throw new ShopStop("4476");
            }

            var phoneStored = string.Empty;
            var emailStored = string.Empty;
            if (userId == 0)
            {
                if (phone is null)
                {
                    throw new ShopStop("4477");
                }

                phoneStored = phone.Trim();
                if (!await ContactMatchesAsync(connection, transaction, phoneStored, "phone", cancellationToken).ConfigureAwait(false))
                {
                    throw new ShopStop("4478");
                }

                if (email is not null)
                {
                    emailStored = email.Trim();
                    if (emailStored.Length > 0 && !await ContactMatchesAsync(connection, transaction, emailStored, "email", cancellationToken).ConfigureAwait(false))
                    {
                        throw new ShopStop("4479");
                    }
                }
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = ErpDb.Positional(
                    "INSERT INTO `shop_orders` (`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`) VALUES (?,?,?,?,?,?,?,?,?,?)");
                ErpDb.AddParameters(insert, userId, sessionId, now, 0, createdStatus, 0, mode, howGetJson, HtmlEntities(phoneStored), HtmlEntities(emailStored));
                if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                {
                    throw new ShopStop("4480");
                }
            }

            var orderId = await ScalarLongAsync(connection, transaction, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
            if (orderId <= 0)
            {
                throw new ShopStop("4481");
            }

            var itemStatus = await ScalarLongAsync(
                connection,
                transaction,
                "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_created` = 1 LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (itemStatus <= 0)
            {
                throw new ShopStop("4482");
            }

            var lines = new List<CheckoutLine>();
            await using (var carts = connection.CreateCommand())
            {
                carts.Transaction = transaction;
                carts.CommandText = ErpDb.Positional(
                    """
                    SELECT `id`, `product_type`, `product_id`, `price`, `count_need`,
                           `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                           `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                           `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                           `t2_product_json`, `t2_json_params`
                    FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ? AND `checked_for_order` = 1
                    """);
                ErpDb.AddParameters(carts, userId, sessionId);
                await using var reader = await carts.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    lines.Add(ReadCheckoutLine(reader));
                }
            }

            var officeExecuter = mode == 1 ? officeId : 0;
            var firstOffice = 0;
            var copied = new List<long>();
            var copiedDetails = new List<long>();
            foreach (var line in lines)
            {
                var purchase = line.ProductType == 2
                    ? line.PricePurchase
                    : (await DetailPurchaseAsync(connection, transaction, line.Id, cancellationToken).ConfigureAwait(false)).ToString(CultureInfo.InvariantCulture);
                if (!OfferAllowsCart(line.Price, purchase, line.Markup))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new ShopStatus(false, CheckoutNoMarginMessage, "no_margin", null);
                }

                copied.Add(line.Id);
                if (firstOffice == 0)
                {
                    firstOffice = line.OfficeId;
                }

                // PHP binds the cart storage id into sao_state and sao_robot for type 1.
                // Type 2 still reads the supplier start-state subqueries.
                var saoState = line.ProductType == 1 ? line.StorageId : 0;
                var saoRobot = line.ProductType == 1 ? line.StorageId : 0;
                if (line.ProductType == 2)
                {
                    saoState = await SaoValueAsync(connection, transaction, line.StorageId, robot: false, cancellationToken).ConfigureAwait(false);
                    saoRobot = await SaoValueAsync(connection, transaction, line.StorageId, robot: true, cancellationToken).ConfigureAwait(false);
                }

                long orderItemId;
                await using (var item = connection.CreateCommand())
                {
                    item.Transaction = transaction;
                    item.CommandText = ErpDb.Positional(
                        """
                        INSERT INTO `shop_orders_items` (
                            `order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,
                            `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                            `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                            `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                            `t2_product_json`, `sao_state`, `sao_robot`, `t2_json_params`
                        ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                        """);
                    ErpDb.AddParameters(
                        item,
                        orderId,
                        line.ProductType,
                        line.Price,
                        line.CountNeed,
                        line.ProductId,
                        itemStatus,
                        line.Manufacturer,
                        line.Article,
                        line.ArticleShow,
                        line.Name,
                        line.Exist,
                        line.TimeToExe,
                        line.TimeGuaranteed,
                        line.Storage,
                        line.MinOrder,
                        line.Probability,
                        line.Markup,
                        line.ProductType == 1 ? "0" : line.PricePurchase,
                        line.OfficeId,
                        line.StorageId,
                        line.ProductType == 1 ? line.ProductJson : string.Empty,
                        saoState,
                        saoRobot,
                        line.ProductType == 1 ? line.JsonParams : line.JsonParams);
                    await item.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                orderItemId = await ScalarLongAsync(connection, transaction, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
                if (orderItemId <= 0)
                {
                    throw new ShopStop("4485");
                }

                if (line.ProductType == 1)
                {
                    copiedDetails.AddRange(await CopyOrderDetailsAsync(connection, transaction, orderId, orderItemId, line.Id, cancellationToken).ConfigureAwait(false));
                    if (officeExecuter == 0)
                    {
                        officeExecuter = await FirstDetailOfficeAsync(connection, transaction, line.Id, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (officeExecuter == 0)
            {
                officeExecuter = firstOffice;
            }

            await using (var finish = connection.CreateCommand())
            {
                finish.Transaction = transaction;
                finish.CommandText = ErpDb.Positional("UPDATE `shop_orders` SET `successfully_created` = 1, `office_id` = ? WHERE `id` = ?");
                ErpDb.AddParameters(finish, officeExecuter, orderId);
                if (await finish.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                {
                    throw new ShopStop("4487");
                }
            }

            if (copied.Count > 0)
            {
                var placeholders = string.Join(",", Enumerable.Repeat("?", copied.Count));
                await using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = ErpDb.Positional("DELETE FROM `shop_carts` WHERE `checked_for_order` = 1 AND `id` IN (" + placeholders + ")");
                ErpDb.AddParameters(delete, copied.Cast<object>().ToArray());
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (copiedDetails.Count > 0)
            {
                var detailPlaceholders = string.Join(",", Enumerable.Repeat("?", copiedDetails.Count));
                await using var details = connection.CreateCommand();
                details.Transaction = transaction;
                details.CommandText = ErpDb.Positional("DELETE FROM `shop_carts_details` WHERE `id` IN (" + detailPlaceholders + ")");
                ErpDb.AddParameters(details, copiedDetails.Cast<object>().ToArray());
                await details.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await LinkActiveGarageAsync(connection, userId, orderId, cancellationToken).ConfigureAwait(false);
            await LogCheckoutEmailsAsync(connection, orderId, userId, emailStored, requestHost, cancellationToken).ConfigureAwait(false);
            return new ShopStatus(true, "4493: " + orderId.ToString(CultureInfo.InvariantCulture), null, (int)orderId);
        }
        catch (ShopStop ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ShopStatus(false, CheckoutFailPrefix + ex.Message, null, null);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new ShopStatus(false, CheckoutFailPrefix + CheckoutMissing(ex), null, null);
        }
    }

    public static async Task<object> OrderMessagesAsync(
        DbConnection connection,
        int userId,
        string? orderIdText,
        string? returnIdText,
        bool manager,
        CancellationToken cancellationToken)
    {
        if (manager)
        {
            return new NumericDenied(false, "Forbidden", 501);
        }

        var denied = await OrderOwnedAsync(connection, userId, orderIdText, returnIdText, cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var rows = new List<OrderMessageRow>();
        await using var command = connection.CreateCommand();
        if (!string.IsNullOrEmpty(returnIdText))
        {
            command.CommandText = ErpDb.Positional("SELECT `time`, `is_customer`, `text` FROM `shop_orders_messages` WHERE `return_id` = ?");
            ErpDb.AddParameters(command, returnIdText);
        }
        else
        {
            command.CommandText = ErpDb.Positional("SELECT `time`, `is_customer`, `text` FROM `shop_orders_messages` WHERE `order_id` = ?");
            ErpDb.AddParameters(command, orderIdText ?? string.Empty);
        }

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var unix = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var when = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                var customer = !reader.IsDBNull(1) && Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0;
                rows.Add(new OrderMessageRow(when, customer, reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ShopStatus(false, OrderMessagesMissing, null, null);
        }

        return rows;
    }

    public static async Task<object> SendOrderMessageAsync(
        DbConnection connection,
        int userId,
        string? orderIdText,
        string? returnIdText,
        string? text,
        bool manager,
        CancellationToken cancellationToken,
        string? requestHost = null)
    {
        if (manager)
        {
            return new NumericDenied(false, "Forbidden", 501);
        }

        var denied = await OrderOwnedAsync(connection, userId, orderIdText, returnIdText, cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var insert = connection.CreateCommand();
            if (!string.IsNullOrEmpty(returnIdText))
            {
                insert.CommandText = ErpDb.Positional(
                    "INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (?,?,?,?,?)");
                ErpDb.AddParameters(insert, 0, 1, HtmlEntities(text), now, returnIdText);
            }
            else
            {
                insert.CommandText = ErpDb.Positional(
                    "INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (?,?,?,?,?)");
                ErpDb.AddParameters(insert, orderIdText ?? "0", 1, HtmlEntities(text), now, 0);
            }

            var rows = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (rows != 1)
            {
                return "false";
            }

            if (string.IsNullOrEmpty(returnIdText))
            {
                await LogStaffMessageEmailAsync(connection, orderIdText, requestHost, cancellationToken).ConfigureAwait(false);
            }

            return "true";
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ShopStatus(false, OrderMessagesMissing, null, null);
        }
    }

    /// <summary>
    /// PHP <c>content/shop/finance/ajax_create_operation.php</c>: a pending income operation for a balance top-up
    /// (<c>3_income_by_customer</c>, signed-in users only) or an order payment (<c>4_income_for_direct_pay</c>, checked
    /// against the order's user, debt, <c>partial_payment</c> and <c>partial_payment_min_percent</c>). The pay system is
    /// the office's under <c>wholesaler</c>, else an enabled <c>pay_handler</c> the customer picked, else the active one;
    /// the payment account from <c>epc_pay_accounts_resolve_for_order</c> brings its own handler and office.
    /// </summary>
    public static async Task<object> CreateOperationAsync(
        DbConnection connection,
        int userId,
        string? requestObjectJson,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? config = null,
        string? cityCookie = null)
    {
        config ??= new Dictionary<string, string>(StringComparer.Ordinal);
        using var document = ParseObject(requestObjectJson);
        if (document is null || !PhpIsset(document.RootElement, "amount"))
        {
            return string.Empty;
        }

        var root = document.RootElement;
        var amount = JsonDecimal(root, "amount");
        var hasOrder = PhpIsset(root, "order_id");
        var operationKey = "3_income_by_customer";
        var payOrder = string.Empty;
        var orderId = hasOrder ? JsonText(root, "order_id") : string.Empty;
        if (!hasOrder)
        {
            if (userId == 0 || amount <= 0)
            {
                return new FinanceResult { Result = false, Message = "Forbidden" };
            }
        }
        else
        {
            try
            {
                int? orderUser = null;
                var paid = 0;
                await using (var order = connection.CreateCommand())
                {
                    order.CommandText = ErpDb.Positional("SELECT `user_id`, `paid` FROM `shop_orders` WHERE `id` = ?");
                    ErpDb.AddParameters(order, orderId);
                    await using var reader = await order.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        orderUser = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                        paid = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                    }
                }

                if ((orderUser ?? 0) != userId)
                {
                    return new FinanceResult { Result = false, User = false };
                }

                if (amount <= 0 || orderUser is null || paid == 1)
                {
                    return new FinanceResult { Result = false, Message = "Forbidden" };
                }

                var skipped = await StatusesNotCountedAsync(connection, cancellationToken).ConfigureAwait(false);
                var sum = Math.Round(await OrderSumAsync(connection, orderId, skipped, cancellationToken).ConfigureAwait(false), 2, MidpointRounding.AwayFromZero);
                var paidSum = Math.Round(await PaidSumAsync(connection, orderId, cancellationToken).ConfigureAwait(false), 2, MidpointRounding.AwayFromZero);
                var left = sum - paidSum;
                if (amount > left)
                {
                    return new FinanceResult { Result = false, Message = "Forbidden" };
                }

                if (amount < left)
                {
                    config.TryGetValue("partial_payment", out var partial);
                    config.TryGetValue("partial_payment_min_percent", out var minPercent);
                    if (!ShopPayForOrderService.PhpTruthy(partial)
                        || amount < sum * ((ShopPayForOrderService.PhpNumber(minPercent) ?? 0m) / 100m))
                    {
                        return new FinanceResult { Result = false, Message = "Forbidden" };
                    }
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new FinanceResult { Result = false, Message = FinanceMissing(ex) };
            }

            operationKey = "4_income_for_direct_pay";
            payOrder = orderId;
        }

        try
        {
            long officeId = 0;
            object? paySystem;
            if (config.ContainsKey("wholesaler"))
            {
                if (hasOrder)
                {
                    officeId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `office_id` FROM `shop_orders` WHERE `id` = ?"), cancellationToken, orderId)
                        .ConfigureAwait(false);
                }
                else
                {
                    officeId = ShopPayForOrderService.PhpIntCast(JsonText(root, "office_id"));
                    if (!(await StorefrontCustomerOffices.LoadAsync(connection, cityCookie, cancellationToken).ConfigureAwait(false)).Contains((int)officeId))
                    {
                        return string.Empty;
                    }
                }

                await using var office = connection.CreateCommand();
                office.CommandText = ErpDb.Positional(
                    "SELECT `handler` FROM `shop_payment_systems` WHERE `id` = (SELECT `pay_system_id` FROM `shop_offices` WHERE `id` = ?) LIMIT 1");
                ErpDb.AddParameters(office, officeId);
                var handler = await office.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                paySystem = handler is null or DBNull ? null : Convert.ToString(handler, CultureInfo.InvariantCulture);
            }
            else
            {
                paySystem = (object?)await ActivePaySystemAsync(connection, root, cancellationToken).ConfigureAwait(false) ?? false;
            }

            IReadOnlyDictionary<string, string?>? account = null;
            long accountId = 0;
            try
            {
                account = await StorefrontPaymentAccounts.ResolveForOrderAsync(
                    connection,
                    hasOrder ? ShopPayForOrderService.PhpIntCast(orderId) : 0,
                    cancellationToken).ConfigureAwait(false);
                if (account is not null)
                {
                    accountId = ShopPayForOrderService.PhpIntCast(account.GetValueOrDefault("id"));
                    var accountHandler = Regex.Replace(account.GetValueOrDefault("handler") ?? string.Empty, "[^a-z0-9_]", string.Empty);
                    if (accountHandler.Length > 0 && PhpEmptyJson(root, "pay_handler"))
                    {
                        paySystem = accountHandler;
                    }

                    var ownerId = ShopPayForOrderService.PhpIntCast(account.GetValueOrDefault("owner_id"));
                    if (account.GetValueOrDefault("owner_type") == "office" && ownerId > 0 && officeId == 0)
                    {
                        officeId = ownerId;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                accountId = 0;
            }

            await using var insert = connection.CreateCommand();
            insert.CommandText = ErpDb.Positional(
                "INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `pay_orders`, `office_id`) VALUES (?, ?, ?, ?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1), ?, ?, ?)");
            ErpDb.AddParameters(insert, userId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 1, amount, operationKey, 0, payOrder, officeId);
            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            {
                return new FinanceResult { Result = false };
            }

            var id = await ScalarLongAsync(connection, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
            if (accountId > 0)
            {
                try
                {
                    await using var link = connection.CreateCommand();
                    link.CommandText = ErpDb.Positional("UPDATE `shop_users_accounting` SET `epc_payment_account_id` = ? WHERE `id` = ?");
                    ErpDb.AddParameters(link, accountId, id);
                    await link.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbException)
                {
                }
            }

            var answer = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = true,
                ["operation"] = id,
                ["pay_system"] = paySystem,
                ["payment_account_id"] = accountId,
            };
            if (account is not null)
            {
                answer["payment_account"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = accountId,
                    ["title"] = account.GetValueOrDefault("title") ?? string.Empty,
                    ["owner_type"] = account.GetValueOrDefault("owner_type") ?? string.Empty,
                    ["owner_id"] = ShopPayForOrderService.PhpIntCast(account.GetValueOrDefault("owner_id")),
                    ["handler"] = account.GetValueOrDefault("handler") ?? string.Empty,
                };
            }

            return answer;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FinanceResult { Result = false, Message = FinanceMissing(ex) };
        }
    }

    /// <summary>PHP <c>isset($a[$key])</c> on a decoded JSON object: present and not null.</summary>
    private static bool PhpIsset(JsonElement root, string key)
        => root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(key, out var value)
            && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    /// <summary>PHP <c>empty($a[$key])</c> on a decoded JSON object.</summary>
    private static bool PhpEmptyJson(JsonElement root, string key)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var value))
        {
            return true;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.False => true,
            JsonValueKind.String => value.GetString() is "" or "0",
            JsonValueKind.Number => value.GetDouble() == 0,
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false,
        };
    }

    public static async Task<object> CheckReturnsAsync(
        DbConnection connection,
        string? itemsId,
        CancellationToken cancellationToken)
    {
        var parts = (itemsId ?? string.Empty).Split(',');
        if (parts.Length == 0)
        {
            parts = [string.Empty];
        }

        var placeholders = string.Join(",", Enumerable.Repeat("?", parts.Length));
        var args = parts.Cast<object>().ToArray();
        try
        {
            var confirm = await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM `shop_orders_returns_items` WHERE `item_id` IN (" + placeholders + ")",
                cancellationToken,
                args).ConfigureAwait(false);
            var complete = await ScalarLongAsync(
                connection,
                "SELECT COUNT(*) FROM `shop_orders_items` WHERE `status` IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `check_for_return` = 1) AND `id` IN (" + placeholders + ")",
                cancellationToken,
                args).ConfigureAwait(false);
            return new ReturnsBody
            {
                Status = true,
                CountConfirm = (int)confirm,
                CountComplete = (int)complete,
                AllComplete = complete == parts.Length
            };
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ReturnsBody { Status = false, Message = ReturnsMissing };
        }
    }

    public static async Task<object> ArticleListAsync(
        DbConnection connection,
        int userId,
        string? shopStatCookie,
        string? requestObjectJson,
        CancellationToken cancellationToken)
    {
        var statIds = CookieIds(shopStatCookie);
        if (userId > 0 && statIds.Count > 0)
        {
            try
            {
                var placeholders = string.Join(",", Enumerable.Repeat("?", statIds.Count));
                await using var update = connection.CreateCommand();
                update.CommandText = ErpDb.Positional("UPDATE `shop_stat_article_queries` SET `user_id` = ? WHERE `id` IN (" + placeholders + ")");
                ErpDb.AddParameters(update, new object[] { userId }.Concat(statIds.Cast<object>()).ToArray());
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new ArticleListBody { Status = false, Message = ArticleQueriesMissing, List = [] };
            }
        }

        using var document = ParseObject(requestObjectJson);
        var value = document is null ? string.Empty : JsonText(document.RootElement, "value");
        var list = new List<ArticleHit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            await AppendStatAsync(connection, userId, value, statIds, list, seen, cancellationToken).ConfigureAwait(false);
            if (value.Length == 0 && statIds.Count == 0 && userId <= 0)
            {
                await ProbeStatAsync(connection, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ArticleListBody { Status = false, Message = ArticleQueriesMissing, List = [] };
        }

        AppendStandards(value, list, seen);
        if (value.Length > 0)
        {
            try
            {
                await AppendPricesAsync(connection, value, list, seen, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new ArticleListBody { Status = false, Message = PriceListsMissing, List = list };
            }

            try
            {
                await AppendCatalogueArticlesAsync(connection, value, list, seen, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new ArticleListBody { Status = false, Message = CatalogueArticlePropertiesMissing, List = list };
            }
        }

        return new ArticleListBody { List = list };
    }

    public static async Task<object> CheckForOrderAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        string? requestObjectJson,
        bool techKeyPath,
        bool techKeyAccepted,
        CancellationToken cancellationToken)
    {
        using var document = ParseObject(requestObjectJson);
        if (document is null || !document.RootElement.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            return new CheckForOrderBody(false, CartStringIncorrectData, "incorrect_data", null);
        }

        if (techKeyPath)
        {
            if (!techKeyAccepted)
            {
                return new CheckForOrderBody(false, CartStringForbidden, "forbidden", null);
            }

            userId = JsonInt(document.RootElement, "user_id");
            sessionRecordId = 0;
        }
        else if (userId <= 0 && sessionRecordId <= 0)
        {
            return new CheckForOrderBody(false, CartStringIncorrectSession, "incorrect_session", null);
        }

        var sessionId = userId > 0 ? 0 : sessionRecordId;
        var ids = records.EnumerateArray().Select(JsonIntElement).ToList();
        if (!techKeyAccepted)
        {
            try
            {
                foreach (var id in ids)
                {
                    if (!await OwnsCartAsync(connection, id, sessionId, userId, cancellationToken).ConfigureAwait(false))
                    {
                        return new CheckForOrderBody(false, CartStringNotFound, "cart_item_not_found", null);
                    }
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new CheckForOrderBody(false, CartMissing, null, null);
            }
        }

        var answer = new List<CheckRow>();
        try
        {
            foreach (var id in ids)
            {
                int current;
                await using (var read = connection.CreateCommand())
                {
                    read.CommandText = ErpDb.Positional("SELECT `checked_for_order` FROM `shop_carts` WHERE `id` = ?");
                    ErpDb.AddParameters(read, id);
                    var scalar = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    current = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
                }

                var next = current == 1 ? 0 : 1;
                await using (var update = connection.CreateCommand())
                {
                    update.CommandText = ErpDb.Positional("UPDATE `shop_carts` SET `checked_for_order` = ? WHERE `id` = ?");
                    ErpDb.AddParameters(update, next, id);
                    await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                answer.Add(new CheckRow(id, next));
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message.Contains("checked_for_order", StringComparison.OrdinalIgnoreCase)
                ? CartCheckMissing
                : CartMissing;
            return new CheckForOrderBody(false, message, null, null);
        }

        return new CheckForOrderBody(true, "Ok", "ok", answer);
    }

    private static async Task<GarageAnswer> SearchCarsAsync(
        DbConnection connection,
        int userId,
        string search,
        CancellationToken cancellationToken)
    {
        var list = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND (`caption` LIKE ? OR `marka` LIKE ? OR `vin` LIKE ? OR `note` LIKE ?)");
        ErpDb.AddParameters(command, userId, "%" + search + "%", search + "%", search + "%", "%" + search + "%");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return new GarageAnswer { Status = true, List = list };
    }

    private static async Task<GarageAnswer> CheckCarAsync(
        DbConnection connection,
        int orderId,
        int carId,
        CancellationToken cancellationToken)
    {
        long existing;
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage_orders` WHERE `order_id` = ? AND `garage_id` = ? LIMIT 1");
            ErpDb.AddParameters(read, orderId, carId);
            var scalar = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            existing = scalar is null or DBNull ? 0 : Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
        }

        if (existing > 0)
        {
            await using var delete = connection.CreateCommand();
            delete.CommandText = ErpDb.Positional("DELETE FROM `shop_docpart_garage_orders` WHERE `order_id` = ? AND `garage_id` = ?");
            ErpDb.AddParameters(delete, orderId, carId);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new GarageAnswer { Status = true, Flag = 0 };
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = ErpDb.Positional("INSERT INTO `shop_docpart_garage_orders` (`garage_id`, `order_id`) VALUES (?,?)");
        ErpDb.AddParameters(insert, carId, orderId);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new GarageAnswer { Status = true, Flag = 1 };
    }

    private static async Task<GarageAnswer> ActiveCarAsync(
        DbConnection connection,
        int postedUserId,
        int carId,
        CancellationToken cancellationToken)
    {
        long previous = 0;
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `active` = 1 LIMIT 1");
            ErpDb.AddParameters(read, postedUserId);
            var scalar = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is not null and not DBNull)
            {
                previous = Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
            }
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.CommandText = ErpDb.Positional("UPDATE `shop_docpart_garage` SET `active` = 0 WHERE `user_id` = ?");
            ErpDb.AddParameters(clear, postedUserId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (previous != carId)
        {
            await using var activate = connection.CreateCommand();
            activate.CommandText = ErpDb.Positional("UPDATE `shop_docpart_garage` SET `active` = 1 WHERE `id` = ?");
            ErpDb.AddParameters(activate, carId);
            await activate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return new GarageAnswer { Status = true };
    }

    private static async Task<string> CarsTableAsync(
        DbConnection connection,
        int customerId,
        int orderId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int Id, string Caption, int MarkId, string Model, string Vin, string Year, int Link)>();
        await using (var command = connection.CreateCommand())
        {
            if (orderId > 0)
            {
                command.CommandText = ErpDb.Positional(
                    """
                    SELECT `id`, `caption`, `mark_id`, `model`, `vin`, `year`,
                           (SELECT COUNT(*) FROM `shop_docpart_garage_orders` WHERE `order_id` = ? AND `garage_id` = `shop_docpart_garage`.`id`)
                    FROM `shop_docpart_garage` WHERE `user_id` = ?
                    """);
                ErpDb.AddParameters(command, orderId, customerId);
            }
            else
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`, `caption`, `mark_id`, `model`, `vin`, `year`, 0 FROM `shop_docpart_garage` WHERE `user_id` = ?");
                ErpDb.AddParameters(command, customerId);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
                    reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
            }
        }

        if (rows.Count == 0)
        {
            return "5609";
        }

        var html = new System.Text.StringBuilder();
        html.Append("<table class=\"table\"><tr>");
        if (orderId > 0)
        {
            html.Append("<th>5608</th>");
        }

        html.Append("<th>630</th><th>4044</th><th>VIN</th><th></th></tr>");
        foreach (var row in rows)
        {
            var mark = row.MarkId > 0 ? await CarMarkAsync(connection, row.MarkId, cancellationToken).ConfigureAwait(false) : string.Empty;
            var markModel = mark;
            if (row.Model.Length > 0)
            {
                if (markModel.Length > 0)
                {
                    markModel += " - ";
                }

                markModel += row.Model;
            }

            html.Append("<tr>");
            if (orderId > 0)
            {
                var color = row.Link > 0 ? "#66bf05" : "#a9a9a9";
                var flag = row.Link > 0 ? 0 : 1;
                html.Append("<td><a style=\"color:").Append(color)
                    .Append("; font-size: 16px;\" onclick=\"check_car(").Append(flag.ToString(CultureInfo.InvariantCulture))
                    .Append(", ").Append(row.Id.ToString(CultureInfo.InvariantCulture))
                    .Append(");\"><i class=\"fa fa-check\" aria-hidden=\"true\"></i></a></td>");
            }

            html.Append("<td><div>").Append(row.Caption).Append("</div><div>").Append(markModel).Append("</div></td><td>")
                .Append(row.Year).Append("</td><td>").Append(row.Vin)
                .Append("</td><td style=\"text-align:right;\"><a onclick=\"edit_car(")
                .Append(row.Id.ToString(CultureInfo.InvariantCulture))
                .Append(");\" class=\"btn btn-ar btn-primary\" title=\"2270\"><i class=\"far fa-edit\"></i></a><a class=\"btn btn-ar btn-primary\" href=\"javascript:void(0);\" onclick=\"delete_car(")
                .Append(row.Id.ToString(CultureInfo.InvariantCulture))
                .Append(");\" title=\"2224\"><i class=\"fa fa-trash\"></i></a></td></tr>");
        }

        html.Append("</table>");
        return html.ToString();
    }

    private static async Task<string> CarMarkAsync(DbConnection connection, int markId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `caption` FROM `shop_docpart_cars` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(command, markId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static async Task<object?> OrderOwnedAsync(
        DbConnection connection,
        int userId,
        string? orderIdText,
        string? returnIdText,
        CancellationToken cancellationToken)
    {
        try
        {
            long count;
            await using var command = connection.CreateCommand();
            if (!string.IsNullOrEmpty(returnIdText))
            {
                command.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders_returns` WHERE `user_id` = ? AND `id` = ?");
                ErpDb.AddParameters(command, userId, returnIdText);
            }
            else
            {
                command.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `user_id` = ? AND `id` = ?");
                ErpDb.AddParameters(command, userId, orderIdText ?? string.Empty);
            }

            count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture);
            return count == 0 ? new NumericDenied(false, "Forbidden", 501) : null;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message.Contains("shop_orders_returns", StringComparison.OrdinalIgnoreCase)
                ? "Order returns are not in this database."
                : OrdersMissing;
            return new ShopStatus(false, message, null, null);
        }
    }

    private static async Task AppendStatAsync(
        DbConnection connection,
        int userId,
        string value,
        IReadOnlyList<int> statIds,
        List<ArticleHit> list,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var args = new List<object>();
        if (value.Length == 0)
        {
            if (statIds.Count > 0)
            {
                where.Add("`id` IN (" + string.Join(",", Enumerable.Repeat("?", statIds.Count)) + ")");
                args.AddRange(statIds.Cast<object>());
            }

            if (userId > 0)
            {
                where.Add("`user_id` = ?");
                args.Add(userId);
            }
        }
        else
        {
            where.Add("(`article` LIKE ? OR `name` LIKE ?)");
            args.Add(value + "%");
            args.Add("%" + value + "%");
        }

        if (where.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `article`, `manufacturer`, `name` FROM `shop_stat_article_queries` WHERE `id` IN (SELECT MAX(`id`) FROM `shop_stat_article_queries` WHERE "
            + string.Join(" AND ", where)
            + " GROUP BY `article`, `manufacturer`) ORDER BY `id` DESC LIMIT 50");
        ErpDb.AddParameters(command, args.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && list.Count < 50)
        {
            AddHit(list, seen, Text(reader, 0), Text(reader, 1), Text(reader, 2));
        }
    }

    private static void AppendStandards(string value, List<ArticleHit> list, HashSet<string> seen)
    {
        ArticleHit[] standards =
        [
            new("C110", "DOLZ", "4194"),
            new("12345", "FEBI", "4195"),
            new("S56545", "BREMBO", "4196")
        ];
        var upper = value.Trim().ToUpperInvariant();
        foreach (var row in standards)
        {
            if (value.Length == 0)
            {
                if (list.Count >= 50)
                {
                    break;
                }
            }
            else if (!row.Article.StartsWith(upper, StringComparison.Ordinal))
            {
                continue;
            }

            AddHit(list, seen, row.Article, row.Manufacturer, row.Name);
        }
    }

    private static async Task AppendPricesAsync(
        DbConnection connection,
        string value,
        List<ArticleHit> list,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        await using (var byArticle = connection.CreateCommand())
        {
            byArticle.CommandText = ErpDb.Positional(
                "SELECT `article`, `manufacturer`, `name` FROM `shop_docpart_prices_data` WHERE `article` LIKE ? LIMIT 50");
            ErpDb.AddParameters(byArticle, value + "%");
            await using var reader = await byArticle.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                AddHit(list, seen, Text(reader, 0), Text(reader, 1), Text(reader, 2));
            }
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 3)
            .ToList();
        await using var byName = connection.CreateCommand();
        if (words.Count == 0)
        {
            byName.CommandText = "SELECT `article`, `manufacturer`, `name` FROM `shop_docpart_prices_data` LIMIT 50";
        }
        else
        {
            byName.CommandText = ErpDb.Positional(
                "SELECT `article`, `manufacturer`, `name` FROM `shop_docpart_prices_data` WHERE "
                + string.Join(" AND ", words.Select(_ => "`name` LIKE ?"))
                + " LIMIT 50");
            ErpDb.AddParameters(byName, words.Select(word => (object)("%" + word + "%")).ToArray());
        }

        await using var nameReader = await byName.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await nameReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddHit(list, seen, Text(nameReader, 0), Text(nameReader, 1), Text(nameReader, 2));
        }
    }

    private static async Task ProbeStatAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id` FROM `shop_stat_article_queries` WHERE 1 = 0";
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AppendCatalogueArticlesAsync(
        DbConnection connection,
        string value,
        List<ArticleHit> list,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        var lang = await WorkLangAsync(connection, cancellationToken).ConfigureAwait(false);
        await using (var articles = connection.CreateCommand())
        {
            articles.CommandText = ErpDb.Positional(
                """
                SELECT `product_id`, `value` FROM `shop_properties_values_text`
                WHERE `property_id` IN (
                    SELECT `id` FROM `shop_categories_properties_map`
                    WHERE `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Артикул')
                      AND `property_type_id` = 3)
                  AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?)
                LIMIT 50
                """);
            ErpDb.AddParameters(articles, value + "%");
            var hits = new List<(int ProductId, string Value)>();
            await using (var reader = await articles.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    hits.Add((
                        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Text(reader, 1)));
                }
            }

            foreach (var hit in hits)
            {
                var manufacturer = await CatalogueManufacturerAsync(connection, hit.ProductId, cancellationToken).ConfigureAwait(false);
                var caption = await CatalogueProductCaptionAsync(connection, hit.ProductId, cancellationToken).ConfigureAwait(false);
                AddHit(
                    list,
                    seen,
                    await TranslateStrAsync(connection, hit.Value, lang, cancellationToken).ConfigureAwait(false),
                    await TranslateStrAsync(connection, manufacturer, lang, cancellationToken).ConfigureAwait(false),
                    await TranslateStrAsync(connection, caption, lang, cancellationToken).ConfigureAwait(false));
            }
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 3)
            .ToList();
        await using var names = connection.CreateCommand();
        if (words.Count == 0)
        {
            names.CommandText = "SELECT `id`, `caption` FROM `shop_catalogue_products` LIMIT 50";
        }
        else
        {
            names.CommandText = ErpDb.Positional(
                "SELECT `id`, `caption` FROM `shop_catalogue_products` WHERE "
                + string.Join(" AND ", words.Select(_ => "`caption` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `value` LIKE ?)"))
                + " LIMIT 50");
            var args = new List<object>();
            foreach (var word in words)
            {
                args.Add(lang);
                args.Add("%" + word + "%");
            }

            ErpDb.AddParameters(names, args.ToArray());
        }

        var products = new List<(int Id, string Caption)>();
        await using (var reader = await names.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                products.Add((Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), Text(reader, 1)));
            }
        }

        foreach (var product in products)
        {
            var articleKey = await CatalogueArticleKeyAsync(connection, product.Id, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(articleKey))
            {
                continue;
            }

            var manufacturer = await CatalogueManufacturerAsync(connection, product.Id, cancellationToken).ConfigureAwait(false);
            AddHit(
                list,
                seen,
                await TranslateStrAsync(connection, articleKey, lang, cancellationToken).ConfigureAwait(false),
                await TranslateStrAsync(connection, manufacturer, lang, cancellationToken).ConfigureAwait(false),
                await TranslateStrAsync(connection, product.Caption, lang, cancellationToken).ConfigureAwait(false));
        }
    }

    private static async Task<string> CatalogueManufacturerAsync(DbConnection connection, int productId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (
                SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = (
                    SELECT `id` FROM `shop_categories_properties_map`
                    WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?)
                      AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Производитель')
                      AND `property_type_id` = 5))
            """);
        ErpDb.AddParameters(command, productId, productId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<string> CatalogueProductCaptionAsync(DbConnection connection, int productId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = ?");
        ErpDb.AddParameters(command, productId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<string> CatalogueArticleKeyAsync(DbConnection connection, int productId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT `value` FROM `shop_properties_values_text` WHERE `property_id` = (
                SELECT `id` FROM `shop_categories_properties_map`
                WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?)
                  AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `lang_code` = 'ru' AND `value` = 'Артикул')
                  AND `property_type_id` = 3) AND `product_id` = ?
            """);
        ErpDb.AddParameters(command, productId, productId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<(string Name, string Manufacturer, string Article, string ArticleShow)?> CatalogueCaptionAsync(
        DbConnection connection,
        int productId,
        CancellationToken cancellationToken)
    {
        string rawName;
        string rawManufacturer;
        string rawArticle;
        await using (var catalogue = connection.CreateCommand())
        {
            catalogue.CommandText = ErpDb.Positional(
                """
                SELECT `caption` AS `name`,
                (SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1) AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ('Производитель', 'Manufacturer')) AND `property_type_id` = 5 LIMIT 1) LIMIT 1) LIMIT 1) AS `manufacturer`,
                (SELECT `value` FROM `shop_properties_values_text` WHERE `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1) AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ('Артикул', 'Article')) AND `property_type_id` = 3 LIMIT 1) AND `product_id` = ?) AS `article`
                FROM `shop_catalogue_products` WHERE `id` = ?
                """);
            ErpDb.AddParameters(catalogue, productId, productId, productId, productId, productId);
            await using var reader = await catalogue.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            rawName = Text(reader, 0);
            rawManufacturer = Text(reader, 1);
            rawArticle = Text(reader, 2);
        }

        var lang = await WorkLangAsync(connection, cancellationToken).ConfigureAwait(false);
        var name = (await TranslateStrAsync(connection, rawName, lang, cancellationToken).ConfigureAwait(false)).Trim();
        var manufacturer = (await TranslateStrAsync(connection, rawManufacturer, lang, cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
        var articleText = await TranslateStrAsync(connection, rawArticle, lang, cancellationToken).ConfigureAwait(false);
        var articleShow = articleText.Trim();
        var article = Regex.Replace(articleText, "[^0-9A-Za-zА-Яа-яЁё]+", string.Empty).ToUpperInvariant();
        return (name, manufacturer, article, articleShow);
    }

    private static async Task<string> WorkLangAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `lang_code` FROM `lang_languages` WHERE `is_default` = 1 LIMIT 1";
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var code = scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
            return code.Length == 0 ? "en" : code;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return "en";
        }
    }

    private static async Task<string> TranslateStrAsync(
        DbConnection connection,
        string? key,
        string lang,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = (SELECT IFNULL(`same`, ?) FROM `lang_text_strings` WHERE `str_key` = ?)");
        ErpDb.AddParameters(command, key, lang, key);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string AdminNotifyEmail(string? host)
    {
        var bare = (host ?? string.Empty).Trim().ToLowerInvariant();
        var colon = bare.IndexOf(':');
        if (colon >= 0)
        {
            bare = bare[..colon];
        }

        if (bare.StartsWith("www.", StringComparison.Ordinal))
        {
            bare = bare[4..];
        }

        if (bare.Length == 0)
        {
            bare = "localhost";
        }

        return "admin@" + bare;
    }

    private static async Task LogCheckoutEmailsAsync(
        DbConnection connection,
        long orderId,
        int userId,
        string emailNotAuth,
        string? requestHost,
        CancellationToken cancellationToken)
    {
        var admin = AdminNotifyEmail(requestHost);
        var customer = userId > 0 ? "user #" + userId.ToString(CultureInfo.InvariantCulture) : emailNotAuth.Trim();
        await LogOrderNotificationAsync(connection, orderId, "Order email to admin " + admin + ": FAILED after retry", cancellationToken).ConfigureAwait(false);
        await LogOrderNotificationAsync(connection, orderId, "Order email to customer (" + customer + "): FAILED", cancellationToken).ConfigureAwait(false);
    }

    private static async Task LogStaffMessageEmailAsync(
        DbConnection connection,
        string? orderIdText,
        string? requestHost,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(orderIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderId) || orderId <= 0)
        {
            return;
        }

        await LogOrderNotificationAsync(
            connection,
            orderId,
            "Order message email to admin " + AdminNotifyEmail(requestHost) + ": FAILED",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task LogOrderNotificationAsync(
        DbConnection connection,
        long orderId,
        string text,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0)
        {
            return;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, 0, 0, ?, 1)");
            ErpDb.AddParameters(command, orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), text);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
        }
    }

    private static async Task<string?> ActivePaySystemAsync(
        DbConnection connection,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var requested = Regex.Replace(JsonText(root, "pay_handler"), "[^a-z0-9_]", string.Empty);
        if (requested.Length > 0)
        {
            await using var picked = connection.CreateCommand();
            picked.CommandText = ErpDb.Positional("SELECT `handler` FROM `shop_payment_systems` WHERE `handler` = ? AND `anable` = 1 LIMIT 1");
            ErpDb.AddParameters(picked, requested);
            var handler = await picked.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (handler is not null and not DBNull)
            {
                return Convert.ToString(handler, CultureInfo.InvariantCulture);
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `handler` FROM `shop_payment_systems` WHERE `active` = 1 LIMIT 1";
        var active = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return active is null or DBNull ? null : Convert.ToString(active, CultureInfo.InvariantCulture);
    }

    private static async Task<List<int>> StatusesNotCountedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static async Task<decimal> OrderSumAsync(
        DbConnection connection,
        string orderId,
        IReadOnlyList<int> skipped,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT SUM(`price` * `count_need`) FROM `shop_orders_items` WHERE `order_id` = ?";
        var args = new List<object> { orderId };
        foreach (var id in skipped)
        {
            sql += " AND `status` != ?";
            args.Add(id);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args.ToArray());
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToDecimal(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> PaidSumAsync(DbConnection connection, string orderId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?), 0)
                 - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)
            """);
        ErpDb.AddParameters(command, orderId, orderId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToDecimal(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<bool> PartialPaymentEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = 'partial_payment' LIMIT 1");
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var raw = Convert.ToString(scalar, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            return raw is "1" or "true" or "yes";
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return false;
        }
    }

    private static async Task<ShopStatus?> TradeBlockAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (await EpcCustomerTrade.CanPlaceOrderAsync(connection, null, userId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var message = await EpcCustomerTrade.CheckoutBlockMessageAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
        return new ShopStatus(false, message, "trade_not_approved", null);
    }

    private static async Task<bool> GuestOrdersAllowedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = 'order_without_auth' LIMIT 1");
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var raw = Convert.ToString(scalar, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (raw.Length == 0)
            {
                return true;
            }

            return raw is not "0" and not "false" and not "no" and not "off";
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return true;
        }
    }

    private static async Task<bool> ContactMatchesAsync(
        DbConnection connection,
        DbTransaction transaction,
        string contact,
        string type,
        CancellationToken cancellationToken)
    {
        if (contact.Length == 0)
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ? LIMIT 1");
        ErpDb.AddParameters(command, type);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var pattern = scalar is null or DBNull ? string.Empty : Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? string.Empty;
        if (pattern.Length == 0)
        {
            return true;
        }

        var match = Regex.Match(contact, pattern);
        return match.Success && match.Value == contact && match.Groups.Count >= 1;
    }

    private static async Task<decimal> DetailPurchaseAsync(
        DbConnection connection,
        DbTransaction transaction,
        long cartId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`price_purchase`, 0) FROM `shop_carts_details` WHERE `cart_record_id` = ? ORDER BY `id` ASC LIMIT 1");
            ErpDb.AddParameters(command, cartId);
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return scalar is null or DBNull ? 0 : Convert.ToDecimal(scalar, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    private static async Task<int> SaoValueAsync(
        DbConnection connection,
        DbTransaction transaction,
        int storageId,
        bool robot,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = robot
                ? ErpDb.Positional(
                    """
                    SELECT IFNULL((SELECT `action_id` FROM `shop_sao_states_types_actions_link` WHERE `is_start` = 1 AND `state_type_id` =
                    (SELECT `id` FROM `shop_sao_states_types_link` WHERE `is_start` = 1 AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?) LIMIT 1) LIMIT 1), 0)
                    """)
                : ErpDb.Positional(
                    """
                    SELECT IFNULL((SELECT `state_id` FROM `shop_sao_states_types_link` WHERE `is_start` = 1 AND `interface_type_id` =
                    (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?) LIMIT 1), 0)
                    """);
            ErpDb.AddParameters(command, storageId);
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    private static async Task<List<long>> CopyOrderDetailsAsync(
        DbConnection connection,
        DbTransaction transaction,
        long orderId,
        long orderItemId,
        long cartId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(long Id, int OfficeId, int StorageId, int StorageRecordId, int Reserved)>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = ErpDb.Positional(
                "SELECT `id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved` FROM `shop_carts_details` WHERE `cart_record_id` = ?");
            ErpDb.AddParameters(read, cartId);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }

        var copied = new List<long>(rows.Count);
        foreach (var row in rows)
        {
            decimal purchase = 0;
            await using (var price = connection.CreateCommand())
            {
                price.Transaction = transaction;
                price.CommandText = ErpDb.Positional(
                    """
                    SELECT `price` * (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`)) AS `price`,
                           `price_purchase` * (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`)) AS `price_purchase`
                    FROM `shop_storages_data` WHERE `id` = ?
                    """);
                ErpDb.AddParameters(price, row.StorageRecordId);
                await using var reader = await price.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var sell = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);
                    var bought = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                    purchase = bought > 0 ? bought : sell;
                }
            }

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = ErpDb.Positional(
                "INSERT INTO `shop_orders_items_details` (`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) VALUES (?,?,?,?,?,?,?,?,?)");
            ErpDb.AddParameters(insert, orderId, orderItemId, row.OfficeId, row.StorageId, row.StorageRecordId, row.Reserved, 0, 0, purchase);
            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            {
                throw new ShopStop("5682");
            }

            copied.Add(row.Id);
        }

        return copied;
    }

    private static async Task<int> FirstDetailOfficeAsync(
        DbConnection connection,
        DbTransaction transaction,
        long cartId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `office_id` FROM `shop_carts_details` WHERE `cart_record_id` = ? ORDER BY `id` ASC LIMIT 1");
        ErpDb.AddParameters(command, cartId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task LinkActiveGarageAsync(
        DbConnection connection,
        int userId,
        long orderId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return;
        }

        try
        {
            long carId;
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `active` = 1 LIMIT 1");
                ErpDb.AddParameters(read, userId);
                var scalar = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                carId = scalar is null or DBNull ? 0 : Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
            }

            if (carId <= 0)
            {
                return;
            }

            await using var insert = connection.CreateCommand();
            insert.CommandText = ErpDb.Positional("INSERT INTO `shop_docpart_garage_orders` (`garage_id`, `order_id`) VALUES (?,?)");
            ErpDb.AddParameters(insert, carId, orderId);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
        }
    }

    private static CheckoutLine ReadCheckoutLine(DbDataReader reader)
        => new(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
            reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "0",
            Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
            Text(reader, 5),
            Text(reader, 6),
            Text(reader, 7),
            Text(reader, 8),
            Text(reader, 9),
            Text(reader, 10),
            Text(reader, 11),
            Text(reader, 12),
            reader.IsDBNull(13) ? 1 : Convert.ToInt32(reader.GetValue(13), CultureInfo.InvariantCulture),
            reader.IsDBNull(14) ? 0 : Convert.ToInt32(reader.GetValue(14), CultureInfo.InvariantCulture),
            Text(reader, 15),
            Text(reader, 16),
            reader.IsDBNull(17) ? 0 : Convert.ToInt32(reader.GetValue(17), CultureInfo.InvariantCulture),
            reader.IsDBNull(18) ? 0 : Convert.ToInt32(reader.GetValue(18), CultureInfo.InvariantCulture),
            Text(reader, 19),
            Text(reader, 20));

    private static string UnescapeCookie(string raw)
    {
        if (!raw.Contains('%', StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            return Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            return raw;
        }
    }

    private static bool TryHowGet(string raw, out int mode, out int officeId, out string json)
    {
        mode = 0;
        officeId = 0;
        json = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            mode = JsonInt(document.RootElement, "mode");
            officeId = JsonInt(document.RootElement, "office_id");
            json = document.RootElement.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CheckoutMissing(Exception ex)
    {
        var message = string.Empty;
        for (var current = ex; current is not null; current = current.InnerException)
        {
            message += " " + current.Message;
        }

        if (message.Contains("checked_for_order", StringComparison.OrdinalIgnoreCase))
        {
            return CartCheckMissing;
        }

        if (message.Contains("shop_orders_items_details", StringComparison.OrdinalIgnoreCase))
        {
            return OrderItemDetailsMissing;
        }

        if (message.Contains("shop_orders_items", StringComparison.OrdinalIgnoreCase))
        {
            return "Order items are not in this database.";
        }

        if (message.Contains("shop_storages_data", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_currencies", StringComparison.OrdinalIgnoreCase)
            || message.Contains("shop_storages", StringComparison.OrdinalIgnoreCase))
        {
            return WarehouseStockMissing;
        }

        if (message.Contains("shop_orders", StringComparison.OrdinalIgnoreCase))
        {
            return OrdersMissing;
        }

        if (message.Contains("shop_carts_details", StringComparison.OrdinalIgnoreCase))
        {
            return CartDetailsMissing;
        }

        if (message.Contains("shop_carts", StringComparison.OrdinalIgnoreCase))
        {
            return CartMissing;
        }

        if (message.Contains("shop_obtaining_modes", StringComparison.OrdinalIgnoreCase))
        {
            return "Obtaining modes are not in this database.";
        }

        return OrdersMissing;
    }

    private static string FinanceMissing(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("shop_payment_systems", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentSystemsMissing;
        }

        if (message.Contains("shop_accounting_codes", StringComparison.OrdinalIgnoreCase) || message.Contains("shop_users_accounting", StringComparison.OrdinalIgnoreCase))
        {
            return AccountingMissing;
        }

        if (message.Contains("shop_orders_items_statuses_ref", StringComparison.OrdinalIgnoreCase))
        {
            return "Order item statuses are not in this database.";
        }

        return AccountingMissing;
    }

    private static async Task<long> ScalarLongAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object[] args)
        => await ScalarLongAsync(connection, null, sql, cancellationToken, args).ConfigureAwait(false);

    private static async Task<long> ScalarLongAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object[] args)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = args.Length == 0 ? sql : ErpDb.Positional(sql);
        if (args.Length > 0)
        {
            ErpDb.AddParameters(command, args);
        }

        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static List<int> CookieIds(string? cookie)
    {
        var ids = new List<int>();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return ids;
        }

        foreach (var part in cookie.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static void AddHit(List<ArticleHit> list, HashSet<string> seen, string article, string manufacturer, string name)
    {
        var key = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(article + manufacturer)));
        if (!seen.Add(key))
        {
            return;
        }

        list.Add(new ArticleHit(article, manufacturer, name));
    }

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static string CleanText(JsonElement? product, string name)
    {
        var raw = product is null ? string.Empty : JsonText(product.Value, name);
        var stripped = Regex.Replace(raw, "<[^>]*>", string.Empty);
        return HtmlEntities(stripped.Trim());
    }

    private static string HtmlEntities(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static decimal JsonDecimal(JsonElement? product, string name)
    {
        if (product is null || !product.Value.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static int JsonInt(JsonElement? product, string name)
        => product is null ? 0 : JsonInt(product.Value, name);

    private sealed class ShopStop(string message) : Exception(message);

    private sealed record CheckoutLine(
        long Id,
        int ProductType,
        int ProductId,
        string Price,
        int CountNeed,
        string Manufacturer,
        string Article,
        string ArticleShow,
        string Name,
        string Exist,
        string TimeToExe,
        string TimeGuaranteed,
        string Storage,
        int MinOrder,
        int Probability,
        string Markup,
        string PricePurchase,
        int OfficeId,
        int StorageId,
        string ProductJson,
        string JsonParams);

    public sealed record NotepadBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message);

    public sealed class GarageAnswer
    {
        [JsonPropertyName("status")]
        public bool Status { get; init; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; init; }

        [JsonPropertyName("list")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? List { get; init; }

        [JsonPropertyName("flag")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Flag { get; init; }
    }

    public sealed record ShopStatus(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message,
        [property: JsonPropertyName("code")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Code,
        [property: JsonPropertyName("order_id")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? OrderId);

    public sealed record NumericDenied(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("code")] int Code);

    public sealed record OrderMessageRow(
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("is_customer")] bool IsCustomer,
        [property: JsonPropertyName("text")] string Text);

    public sealed class FinanceResult
    {
        [JsonPropertyName("result")]
        public bool Result { get; init; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; init; }

        [JsonPropertyName("user")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? User { get; init; }

        [JsonPropertyName("operation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Operation { get; init; }

        [JsonPropertyName("pay_system")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? PaySystem { get; init; }

        [JsonPropertyName("payment_account_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PaymentAccountId { get; init; }
    }

    public sealed class ReturnsBody
    {
        [JsonPropertyName("status")]
        public bool Status { get; init; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; init; }

        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Code { get; init; }

        [JsonPropertyName("count_confirm")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CountConfirm { get; init; }

        [JsonPropertyName("count_complete")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CountComplete { get; init; }

        [JsonPropertyName("all_complete")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? AllComplete { get; init; }
    }

    public sealed class ArticleListBody
    {
        [JsonPropertyName("list")]
        public IReadOnlyList<ArticleHit> List { get; init; } = [];

        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Status { get; init; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; init; }
    }

    public sealed record ArticleHit(
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("name")] string Name);

    public sealed record CheckForOrderBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message,
        [property: JsonPropertyName("code")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Code,
        [property: JsonPropertyName("records")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<CheckRow>? Records);

    public sealed record CheckRow(
        [property: JsonPropertyName("cart_record_id")] int CartRecordId,
        [property: JsonPropertyName("checked_for_order")] int CheckedForOrder);
}
