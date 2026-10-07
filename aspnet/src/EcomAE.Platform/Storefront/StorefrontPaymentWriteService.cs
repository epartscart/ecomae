using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live twins of PHP <c>ajax_create_operation.php</c>, demo <c>go_to_pay</c>,
/// <c>notification.php</c>, and <c>protocol/pay_for_order.php</c> (initiator=3).
/// Card-capture acquirer APIs stay unconfigured the same way PHP demo stubs are.
/// </summary>
public interface IStorefrontPaymentWriteService
{
    Task<StorefrontPaymentWriteResult> CreateOperationAsync(
        int userId,
        decimal amount,
        long orderId,
        string? payHandler,
        CancellationToken cancellationToken = default);

    Task<StorefrontPaymentWriteResult> NotifyAsync(
        int userId,
        long operationId,
        decimal sum,
        string? demoToken,
        string? handler,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>my_order.php</c> action <c>pay_on_place</c>: set <c>paid_type=1</c>, order log, then the robot status protocol to the <c>for_paid</c> status.</summary>
    Task<StorefrontPaymentWriteResult> PayOnPlaceAsync(
        int userId,
        long orderId,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontPaymentWriteResult(
    bool Ok,
    string Code,
    string Message,
    long Id,
    string? PaySystem,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        surface = "storefront",
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = false,
        phpAuthoritative = false,
        validation_code = Code,
        message = Message,
        operation = Id,
        pay_system = PaySystem,
        session
    };
}

public sealed class StorefrontPaymentWriteService : IStorefrontPaymentWriteService
{
    public const string DemoToken = "epc-demo-ok";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IShopOrderProtocolService? _protocol;
    private readonly IStorefrontNotifyDispatcher? _notify;
    private readonly EcomAE.Platform.Cp.ICpPlatformMailer? _mailer;

    public StorefrontPaymentWriteService(
        IErpWriteConnectionFactory connections,
        IShopOrderProtocolService? protocol = null,
        IStorefrontNotifyDispatcher? notify = null,
        EcomAE.Platform.Cp.ICpPlatformMailer? mailer = null)
    {
        _connections = connections;
        _protocol = protocol;
        _notify = notify;
        _mailer = mailer;
    }

    public async Task<StorefrontPaymentWriteResult> CreateOperationAsync(
        int userId,
        decimal amount,
        long orderId,
        string? payHandler,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Forbidden");
        }

        if (amount <= 0)
        {
            return Fail("invalid", "Forbidden");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        var handler = SanitizeHandler(payHandler);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        string operationKey;
        var payOrders = "";
        var officeId = 0L;
        if (orderId > 0)
        {
            var owner = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `user_id` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
            if (owner != userId)
            {
                return Fail("forbidden", "Forbidden");
            }

            var paid = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `paid` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
            if (paid == 1)
            {
                return Fail("forbidden", "Forbidden");
            }

            var orderSum = await OrderSumAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            var paidSum = await PaidSumAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            var paidLeft = orderSum - paidSum;
            if (amount > paidLeft)
            {
                return Fail("forbidden", "Forbidden");
            }

            operationKey = "4_income_for_direct_pay";
            payOrders = orderId.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            operationKey = "3_income_by_customer";
        }

        if (handler.Length == 0)
        {
            handler = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `handler` FROM `shop_payment_systems` WHERE `active`=1 LIMIT 1"),
                cancellationToken).ConfigureAwait(false) ?? "epc_demo";
        }

        var codeId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key`=? LIMIT 1"),
            cancellationToken,
            operationKey).ConfigureAwait(false);
        if (codeId <= 0)
        {
            return Fail("invalid", "Forbidden");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_users_accounting` (`user_id`,`time`,`income`,`amount`,`operation_code`,`active`,`pay_orders`,`office_id`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            (long)userId, now, 1, amount, codeId, 0, payOrders, officeId).ConfigureAwait(false);
        var opId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new StorefrontPaymentWriteResult(true, "ok", "Operation created", opId, handler, 1);
    }

    public async Task<StorefrontPaymentWriteResult> PayOnPlaceAsync(
        int userId,
        long orderId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Please log in or register to continue.");
        }

        if (orderId <= 0)
        {
            return Fail("invalid", "Order is required.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var owner = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (owner != userId)
        {
            return Fail("not_found", "Order is not in your account.");
        }

        var paidType = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`paid_type`, 0) FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (paidType != 0)
        {
            return Fail("already", "Pay on place is already set for this order.");
        }

        var officeId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`office_id`, 0) FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (officeId > 0)
        {
            var office = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_offices` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                officeId).ConfigureAwait(false);
            if (office <= 0)
            {
                return Fail("office", "Order office is not available for pay on place.");
            }
        }

        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_orders` SET `paid_type`=1 WHERE `id`=? AND `user_id`=? AND `paid_type`=0"),
            cancellationToken,
            orderId, userId).ConfigureAwait(false);
        if (rows <= 0)
        {
            return Fail("not_found", "Order was not updated.");
        }

        var writes = 1;
        var paidName = "Pay on place";
        try
        {
            var name = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `name` FROM `shop_orders_paid_type` WHERE `id`=1 AND `active`=1 LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(name))
            {
                paidName = name.Trim();
            }
        }
        catch
        {
            // Caption table is optional on throwaway DBs.
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                orderId, now, (long)userId, 0, "Способ оплаты: <b>" + paidName + "</b>", 0).ConfigureAwait(false);
            writes++;
        }
        catch
        {
            // Log table is optional on throwaway DBs.
        }

        var message = "Pay on place saved.";
        try
        {
            var forPaidStatus = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid`=1 LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
            if (forPaidStatus > 0)
            {
                if (_protocol is not null)
                {
                    var changed = await _protocol.SetOrderStatusAsync(connection, [orderId], forPaidStatus, ShopProtocolActor.Robot, cancellationToken).ConfigureAwait(false);
                    if (changed.Status)
                    {
                        writes++;
                    }
                    else
                    {
                        message = "Pay on place saved. Order status was not changed.";
                    }
                }
                else if (await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_orders` SET `status`=? WHERE `id`=? AND `user_id`=?"),
                    cancellationToken,
                    forPaidStatus, orderId, userId).ConfigureAwait(false) > 0)
                {
                    writes++;
                }
            }
        }
        catch (System.Data.Common.DbException)
        {
            // Missing status ref table is optional on throwaway DBs.
        }

        return new StorefrontPaymentWriteResult(true, "ok", message, orderId, null, writes);
    }

    public async Task<StorefrontPaymentWriteResult> NotifyAsync(
        int userId,
        long operationId,
        decimal sum,
        string? demoToken,
        string? handler,
        CancellationToken cancellationToken = default)
    {
        if ((demoToken ?? string.Empty).Trim() != DemoToken)
        {
            return Fail("forbidden", "Forbidden");
        }

        if (operationId <= 0)
        {
            return Fail("invalid", "Forbidden");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var translator = new StorefrontPhpTranslator(connection);
        var strings = new Dictionary<int, string>();
        foreach (var key in new[] { 1316, 4366, 4529, 3584, 3515 })
        {
            strings[key] = await translator.TextAsync(key, cancellationToken).ConfigureAwait(false);
        }

        PayApplied? applied = null;
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pending = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id`=? AND `active`=0"),
                cancellationToken,
                operationId).ConfigureAwait(false);
            if (pending != 1)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail("forbidden", "Forbidden");
            }

            var opUser = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `user_id` FROM `shop_users_accounting` WHERE `id`=?"),
                cancellationToken,
                operationId).ConfigureAwait(false);
            if (userId > 0 && opUser != userId)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Fail("forbidden", "Forbidden");
            }

            var opAmount = await ErpDb.DecimalAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `amount` FROM `shop_users_accounting` WHERE `id`=?"),
                cancellationToken,
                operationId).ConfigureAwait(false);
            var applySum = sum > 0 ? sum : opAmount;
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `shop_users_accounting` SET `active`=1 WHERE `id`=?"),
                cancellationToken,
                operationId).ConfigureAwait(false);

            var payOrders = await ErpDb.StringAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `pay_orders` FROM `shop_users_accounting` WHERE `id`=?"),
                cancellationToken,
                operationId).ConfigureAwait(false) ?? "";
            if (long.TryParse(payOrders, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderId) && orderId > 0)
            {
                var (result, done) = await ApplyPayForOrderAsync(connection, tx, orderId, applySum, strings, cancellationToken).ConfigureAwait(false);
                if (!result.Ok)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return result;
                }

                applied = done;
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (applied is not null)
        {
            await AfterPayForOrderAsync(connection, translator, applied, cancellationToken).ConfigureAwait(false);
        }

        return new StorefrontPaymentWriteResult(true, "ok", "Payment applied", operationId, SanitizeHandler(handler), 1);
    }

    /// <summary>What PHP pay_for_order.php still needs after the commit.</summary>
    private sealed record PayApplied(long OrderId, long UserId, long OfficeId, decimal PaySum, decimal OrderSum, decimal PaidLeftNew, int NewPaid, string NewPaidText);

    /// <summary>
    /// PHP pay_for_order.php after <c>commit()</c> for <c>initiator=3</c> (payment system): <c>paid_type=3</c> with its
    /// log line, <c>order_pay_to_manager</c> (raw office <c>users</c> list) and <c>order_pay_to_customer</c>, then the
    /// robot status protocol to the <c>for_paid</c> status once the order is fully paid. Failures are not reported, like PHP.
    /// </summary>
    private async Task AfterPayForOrderAsync(System.Data.Common.DbConnection connection, StorefrontPhpTranslator translator, PayApplied pay, CancellationToken cancellationToken)
    {
        try
        {
            var cardName = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `name` FROM `shop_orders_paid_type` WHERE `id` = 3 AND `active` = 1 LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders` SET `paid_type` = 3 WHERE `id` = ?"), cancellationToken, pay.OrderId)
                .ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                pay.OrderId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                0,
                0,
                await translator.TextAsync(4645, cancellationToken).ConfigureAwait(false) + ": <b>"
                    + (cardName is null ? string.Empty : await translator.TextAsync(cardName, cancellationToken).ConfigureAwait(false)) + "</b>",
                1).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
        }

        if (_notify is not null)
        {
            try
            {
                await NotifyPaymentAsync(connection, translator, pay, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
            }
        }

        if (pay.NewPaid == 1 && _protocol is not null)
        {
            try
            {
                var forPaid = await ErpDb.LongAsync(
                    connection,
                    null,
                    "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1 LIMIT 1",
                    cancellationToken).ConfigureAwait(false);
                if (forPaid > 0)
                {
                    await _protocol.SetOrderStatusAsync(connection, [pay.OrderId], forPaid, ShopProtocolActor.Robot, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (System.Data.Common.DbException)
            {
            }
        }
    }

    private async Task NotifyPaymentAsync(System.Data.Common.DbConnection connection, StorefrontPhpTranslator translator, PayApplied pay, CancellationToken cancellationToken)
    {
        var config = _mailer?.ReadConfig() ?? new Dictionary<string, string>();
        var domain = config.TryGetValue("domain_path", out var d) ? d : string.Empty;
        var backend = config.TryGetValue("backend_dir", out var b) ? b : string.Empty;
        var color = "#799658";
        try
        {
            var raw = await ErpDb.StringAsync(connection, null, "SELECT `data_value` FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1", cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("main_color", out var main)
                    && main.ValueKind == System.Text.Json.JsonValueKind.String
                    && !string.IsNullOrEmpty(main.GetString())
                    && main.GetString() != "0")
                {
                    color = main.GetString()!;
                }
            }
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or System.Text.Json.JsonException)
        {
        }

        var linkText = await translator.TextAsync(4643, cancellationToken).ConfigureAwait(false);
        string Link(string href)
            => "<div style=\"margin-top:10px;\"><a style=\"background: " + color
                + "; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;\" target=\"_blank\" href=\""
                + href + "\">" + linkText + "</a></div>";

        var id = pay.OrderId.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> Vars(string link) => new(StringComparer.Ordinal)
        {
            ["order_id"] = id,
            ["pay_value"] = pay.PaySum.ToString(CultureInfo.InvariantCulture),
            ["paid"] = pay.NewPaidText,
            ["order_sum"] = pay.OrderSum.ToString(CultureInfo.InvariantCulture),
            ["paid_sum"] = ShopOrderProtocolService.PhpFloat(pay.OrderSum - pay.PaidLeftNew),
            ["paid_left"] = ShopOrderProtocolService.PhpFloat(pay.PaidLeftNew),
            ["order_link"] = link,
        };

        var managers = new List<StorefrontNotifyPerson>();
        var users = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `users` FROM `shop_offices` WHERE `id` = ?"), cancellationToken, pay.OfficeId)
            .ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(users))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(users);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        var raw = element.ValueKind == System.Text.Json.JsonValueKind.String ? element.GetString() : element.GetRawText();
                        _ = int.TryParse(new string((raw ?? string.Empty).Trim().TakeWhile(char.IsAsciiDigit).ToArray()), NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid);
                        managers.Add(StorefrontNotifyPerson.User(uid));
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        await _notify!.SendAsync(connection, "order_pay_to_manager", Vars(Link(domain + backend + "/shop/orders/order?order_id=" + id)), managers, cancellationToken)
            .ConfigureAwait(false);

        var guest = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT CONCAT(IFNULL(`email_not_auth`, ''), CHAR(1), IFNULL(`phone_not_auth`, '')) FROM `shop_orders` WHERE `id` = ?"),
            cancellationToken,
            pay.OrderId).ConfigureAwait(false) ?? "\u0001";
        var parts = guest.Split('\u0001');
        StorefrontNotifyPerson customer = pay.UserId > 0
            ? StorefrontNotifyPerson.User((int)pay.UserId)
            : StorefrontNotifyPerson.Direct(parts[0], parts.Length > 1 ? parts[1] : string.Empty);
        var customerLink = Link(domain + (pay.UserId > 0 ? "shop/orders/order?order_id=" : "shop/orders/zakaz-bez-registracii?order_id=") + id);
        await _notify.SendAsync(connection, "order_pay_to_customer", Vars(customerLink), [customer], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(StorefrontPaymentWriteResult Result, PayApplied? Applied)> ApplyPayForOrderAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction tx,
        long orderId,
        decimal paySum,
        IReadOnlyDictionary<int, string> strings,
        CancellationToken cancellationToken)
    {
        if (paySum <= 0)
        {
            return (Fail("forbidden", "Forbidden"), null);
        }

        var paid = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `paid` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (paid == 1)
        {
            return (Fail("forbidden", "Forbidden"), null);
        }

        var orderUser = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `user_id` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        var officeId = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `office_id` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        var orderSum = await OrderSumAsync(connection, orderId, cancellationToken, tx).ConfigureAwait(false);
        var paidSum = await PaidSumAsync(connection, orderId, cancellationToken, tx).ConfigureAwait(false);
        var paidLeft = orderSum - paidSum;
        if (paySum > paidLeft)
        {
            return (Fail("forbidden", "Forbidden"), null);
        }

        var expenseCode = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key`=? LIMIT 1"),
            cancellationToken,
            "1_pay_for_order").ConfigureAwait(false);
        if (expenseCode <= 0)
        {
            return (Fail("invalid", "Forbidden"), null);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "INSERT INTO `shop_users_accounting` (`user_id`,`time`,`income`,`amount`,`operation_code`,`active`,`order_id`,`office_id`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            orderUser, now, 0, paySum, expenseCode, 1, orderId, officeId).ConfigureAwait(false);

        var paidLeftNew = paidLeft - paySum;
        var newPaid = paidLeftNew == 0 ? 1 : 2;
        if (paidLeftNew < 0)
        {
            return (Fail("forbidden", "Forbidden"), null);
        }

        var newPaidText = strings[newPaid == 1 ? 3584 : 3515];

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `shop_orders` SET `paid`=? WHERE `id`=?"),
            cancellationToken,
            newPaid, orderId).ConfigureAwait(false);
        try
        {
            var logText = strings[1316] + ". " + strings[4366] + " <b>" + paySum.ToString(CultureInfo.InvariantCulture)
                          + "</b><br/>" + strings[4529] + ": <b>" + newPaidText + "</b>";
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                orderId, now, 0L, 0, logText, 1).ConfigureAwait(false);
        }
        catch
        {
            // Log table is optional on throwaway DBs.
        }

        return (
            new StorefrontPaymentWriteResult(true, "ok", "Payment applied", orderId, "epc_demo", 1),
            new PayApplied(orderId, orderUser, officeId, paySum, orderSum, paidLeftNew, newPaid, newPaidText));
    }

    private static async Task<decimal> OrderSumAsync(
        System.Data.Common.DbConnection connection,
        long orderId,
        CancellationToken cancellationToken,
        System.Data.Common.DbTransaction? tx = null)
    {
        try
        {
            return await ErpDb.DecimalAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "SELECT CAST(SUM(`price`*`count_need`) AS DECIMAL(8,2)) FROM `shop_orders_items` WHERE `order_id`=? AND `status` NOT IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag`=0)"),
                cancellationToken,
                orderId).ConfigureAwait(false);
        }
        catch
        {
            return await ErpDb.DecimalAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT CAST(SUM(`price`*`count_need`) AS DECIMAL(8,2)) FROM `shop_orders_items` WHERE `order_id`=?"),
                cancellationToken,
                orderId).ConfigureAwait(false);
        }
    }

    private static Task<decimal> PaidSumAsync(
        System.Data.Common.DbConnection connection,
        long orderId,
        CancellationToken cancellationToken,
        System.Data.Common.DbTransaction? tx = null)
        => ErpDb.DecimalAsync(
            connection,
            tx,
            ErpDb.Positional(
                "SELECT CAST((IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=0 AND `order_id`=?),0) - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=1 AND `order_id`=?),0)) AS DECIMAL(8,2))"),
            cancellationToken,
            orderId, orderId);

    public static string SanitizeHandler(string? handler)
    {
        var raw = (handler ?? string.Empty).Trim().ToLowerInvariant();
        var chars = raw.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_').ToArray();
        return new string(chars);
    }

    private static StorefrontPaymentWriteResult Fail(string code, string message)
        => new(false, code, message, 0, null, 0);
}
