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

    public StorefrontPaymentWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
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
                var applied = await ApplyPayForOrderAsync(connection, tx, orderId, applySum, cancellationToken).ConfigureAwait(false);
                if (!applied.Ok)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return applied;
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new StorefrontPaymentWriteResult(true, "ok", "Payment applied", operationId, SanitizeHandler(handler), 1);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<StorefrontPaymentWriteResult> ApplyPayForOrderAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction tx,
        long orderId,
        decimal paySum,
        CancellationToken cancellationToken)
    {
        if (paySum <= 0)
        {
            return Fail("forbidden", "Forbidden");
        }

        var paid = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `paid` FROM `shop_orders` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (paid == 1)
        {
            return Fail("forbidden", "Forbidden");
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
            return Fail("forbidden", "Forbidden");
        }

        var expenseCode = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key`=? LIMIT 1"),
            cancellationToken,
            "1_pay_for_order").ConfigureAwait(false);
        if (expenseCode <= 0)
        {
            return Fail("invalid", "Forbidden");
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
            return Fail("forbidden", "Forbidden");
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `shop_orders` SET `paid`=? WHERE `id`=?"),
            cancellationToken,
            newPaid, orderId).ConfigureAwait(false);
        try
        {
            var logText = "Payment. Amount <b>" + paySum.ToString("0.00", CultureInfo.InvariantCulture)
                          + "</b><br/>Paid status: <b>" + (newPaid == 1 ? "Paid" : "Partial") + "</b>";
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

        return new StorefrontPaymentWriteResult(true, "ok", "Payment applied", orderId, "epc_demo", 1);
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
