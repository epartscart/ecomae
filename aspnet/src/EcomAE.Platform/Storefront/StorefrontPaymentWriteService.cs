using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live twins of PHP <c>ajax_create_operation.php</c>, demo <c>go_to_pay</c> and the shared gateway
/// <c>notification.php</c> (activate the operation, <c>pay_notify.php</c>, then <c>protocol/pay_for_order.php</c>
/// with initiator=3 through <see cref="IShopPayForOrderService"/>).
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
    private readonly IErpAdvanceVatService? _advanceVat;
    private IShopPayForOrderService? _payForOrder;

    public StorefrontPaymentWriteService(
        IErpWriteConnectionFactory connections,
        IShopOrderProtocolService? protocol = null,
        IStorefrontNotifyDispatcher? notify = null,
        EcomAE.Platform.Cp.ICpPlatformMailer? mailer = null,
        IShopPayForOrderService? payForOrder = null,
        IErpAdvanceVatService? advanceVat = null)
    {
        _connections = connections;
        _protocol = protocol;
        _notify = notify;
        _mailer = mailer;
        _payForOrder = payForOrder;
        _advanceVat = advanceVat;
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
        var pending = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;"),
            cancellationToken,
            operationId).ConfigureAwait(false);
        if (pending != 1)
        {
            return Fail("forbidden", "Forbidden");
        }

        var opUser = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `shop_users_accounting` WHERE `id`=?"),
            cancellationToken,
            operationId).ConfigureAwait(false);
        if (userId > 0 && opUser != userId)
        {
            return Fail("forbidden", "Forbidden");
        }

        var opAmount = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `amount` FROM `shop_users_accounting` WHERE `id`=?"),
            cancellationToken,
            operationId).ConfigureAwait(false) ?? "0";
        var amount = sum > 0 ? StorefrontOrderNotificationService.PhpFloat(sum) : opAmount;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;"),
            cancellationToken,
            operationId).ConfigureAwait(false);
        var writes = 1;

        if (_notify is not null)
        {
            try
            {
                await PayBySiteAsync(connection, operationId, amount, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
            }
        }

        var message = "Payment applied";
        var payOrders = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `pay_orders` FROM `shop_users_accounting` WHERE `id` = ?;"),
            cancellationToken,
            operationId).ConfigureAwait(false) ?? string.Empty;
        if (payOrders.Length > 0)
        {
            var config = ShopPayForOrderConfig.From(_mailer?.ReadConfig() ?? new Dictionary<string, string>());
            var paid = await PayForOrder.PayAsync(
                connection,
                new ShopPayForOrderRequest(ShopPayForOrderService.InitiatorPaymentSystem, payOrders, amount, "0", 0, 0, true, config),
                cancellationToken).ConfigureAwait(false);
            if (paid.Status)
            {
                writes++;
            }
            else
            {
                message = "Payment credited to the balance; the order was not paid: " + paid.Message;
            }
        }

        return new StorefrontPaymentWriteResult(true, "ok", message, operationId, SanitizeHandler(handler), writes);
    }

    private IShopPayForOrderService PayForOrder
        => _payForOrder ??= new ShopPayForOrderService(_notify, _protocol, _advanceVat);

    /// <summary>
    /// PHP <c>finance/pay_notify.php</c>: <c>pay_by_site</c> to the managers of the operation's office, or of every
    /// office when the operation has none, each manager once.
    /// </summary>
    private async Task PayBySiteAsync(System.Data.Common.DbConnection connection, long operationId, string amount, CancellationToken cancellationToken)
    {
        var officeId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `office_id` FROM `shop_users_accounting` WHERE `id` = ?;"),
            cancellationToken,
            operationId).ConfigureAwait(false);
        var usersLists = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `users` FROM `shop_offices` " + (officeId > 0 ? " WHERE `id` = ? " : string.Empty) + ";");
            if (officeId > 0)
            {
                ErpDb.AddParameters(command, officeId);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                usersLists.Add(reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        var seen = new HashSet<int>();
        var persons = new List<StorefrontNotifyPerson>();
        foreach (var users in usersLists)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(users);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var raw = element.ValueKind == System.Text.Json.JsonValueKind.String ? element.GetString() : element.GetRawText();
                    var uid = (int)ShopPayForOrderService.PhpIntCast(raw);
                    if (seen.Add(uid))
                    {
                        persons.Add(StorefrontNotifyPerson.User(uid));
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation_id"] = operationId.ToString(CultureInfo.InvariantCulture),
            ["amount"] = amount,
        };
        await _notify!.SendAsync(connection, "pay_by_site", vars, persons, cancellationToken).ConfigureAwait(false);
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
