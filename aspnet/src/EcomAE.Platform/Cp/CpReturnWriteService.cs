using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_return_action.php</c> twins for set_return_status / decide_line / finalize_return.
/// Does not seed missing statuses or lang strings (PHP <c>epc_returns_ensure_automation</c> stays PHP).
/// </summary>
public interface ICpReturnWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long returnId, int statusId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DecideLineAsync(long returnId, long lineId, int decide, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> FinalizeAsync(long returnId, int adminUserId, CancellationToken cancellationToken = default);
}

public sealed class CpReturnWriteService : ICpReturnWriteService
{
    private static readonly string[] ClosedCaptions = ["3798", "epc_ret_st_closed"];
    private static readonly string[] OpenCaptions = ["3806", "3796", "epc_ret_st_under_consideration", "epc_ret_st_created"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpReturnWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long returnId,
        int statusId,
        CancellationToken cancellationToken = default)
    {
        if (returnId <= 0 || statusId < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid status.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection, null, ErpDb.Positional("SELECT `id` FROM `shop_orders_returns` WHERE `id` = ? LIMIT 1"),
            cancellationToken, returnId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Return not found.");
        }

        var closedId = await StatusIdByCaptionsAsync(connection, ClosedCaptions, cancellationToken).ConfigureAwait(false);
        var complete = statusId == closedId ? 1 : 0;
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = ? WHERE `id` = ?"),
            cancellationToken, statusId, complete, returnId);
        return ErpSimpleWriteResult.Ok("Return status updated.", returnId);
    }

    public async Task<ErpSimpleWriteResult> DecideLineAsync(
        long returnId,
        long lineId,
        int decide,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (returnId <= 0 || lineId <= 0 || decide is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid line decision.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var itemId = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `item_id` FROM `shop_orders_returns_items` WHERE `id` = ? AND `return_id` = ? LIMIT 1"),
            cancellationToken, lineId, returnId);
        if (itemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Line not found.");
        }

        var completeStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `complete_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        var rejectStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `reject_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        var newStatus = decide == 1 ? completeStatus : rejectStatus;
        if (newStatus <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "Return item statuses are not configured.");
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_returns_items` SET `return_success` = ? WHERE `id` = ?"),
            cancellationToken, decide, lineId);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?"),
            cancellationToken, newStatus, itemId);

        var orderId = await ErpDb.LongAsync(
            connection, null, ErpDb.Positional("SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1"),
            cancellationToken, itemId);
        if (orderId > 0)
        {
            var text = decide == 1
                ? "Return line approved for item [" + itemId.ToString(CultureInfo.InvariantCulture) + "] on return #" + returnId.ToString(CultureInfo.InvariantCulture)
                : "Return line denied for item [" + itemId.ToString(CultureInfo.InvariantCulture) + "] on return #" + returnId.ToString(CultureInfo.InvariantCulture);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
                cancellationToken, orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, text);
        }

        var openId = await StatusIdByCaptionsAsync(connection, OpenCaptions, cancellationToken).ConfigureAwait(false);
        if (openId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 0 WHERE `id` = ? AND (`return_complete` IS NULL OR `return_complete` = 0)"),
                cancellationToken, openId, returnId);
        }

        return ErpSimpleWriteResult.Ok("Return line decided.", lineId);
    }

    public async Task<ErpSimpleWriteResult> FinalizeAsync(
        long returnId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (returnId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid return.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var pending = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("""
                SELECT COUNT(*) FROM `shop_orders_returns_items`
                WHERE `return_id` = ? AND (`return_success` IS NULL
                    OR (`return_success` NOT IN (0,1) AND `return_success` NOT IN ('0','1')))
                """),
            cancellationToken, returnId);
        if (pending > 0)
        {
            return ErpSimpleWriteResult.Fail("pending", "Decide every line (Approve or Deny) before closing.");
        }

        var completeStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `complete_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        var rejectStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `reject_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        if (completeStatus <= 0 || rejectStatus <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "Return item statuses are not configured.");
        }

        await using var lines = connection.CreateCommand();
        lines.CommandText = ErpDb.Positional("SELECT `item_id`, `return_success` FROM `shop_orders_returns_items` WHERE `return_id` = ?");
        ErpDb.AddParameters(lines, returnId);
        await using var reader = await lines.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var itemStatuses = new List<(long ItemId, int Decide)>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = Convert.ToInt64(reader["item_id"], CultureInfo.InvariantCulture);
            var raw = Convert.ToString(reader["return_success"], CultureInfo.InvariantCulture) ?? "0";
            itemStatuses.Add((itemId, raw is "1" ? 1 : 0));
        }

        await reader.CloseAsync().ConfigureAwait(false);
        foreach (var (itemId, decide) in itemStatuses)
        {
            if (itemId <= 0)
            {
                continue;
            }

            var newStatus = decide == 1 ? completeStatus : rejectStatus;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?"),
                cancellationToken, newStatus, itemId);
        }

        var approvedSum = await ErpDb.DecimalAsync(
            connection, null,
            ErpDb.Positional("""
                SELECT COALESCE(SUM(oi.`price` * oi.`count_need`), 0) FROM `shop_orders_returns_items` ri
                INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                WHERE ri.`return_id` = ? AND ri.`return_success` IN (1,'1')
                """),
            cancellationToken, returnId);
        var closedId = await StatusIdByCaptionsAsync(connection, ClosedCaptions, cancellationToken).ConfigureAwait(false);
        if (closedId <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "Closed return status is not configured.");
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 1, `sum` = ? WHERE `id` = ?"),
            cancellationToken, closedId, approvedSum, returnId);

        await using var orders = connection.CreateCommand();
        orders.CommandText = ErpDb.Positional("""
            SELECT DISTINCT oi.`order_id` FROM `shop_orders_returns_items` ri
            INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
            WHERE ri.`return_id` = ? AND oi.`order_id` > 0
            """);
        ErpDb.AddParameters(orders, returnId);
        await using var orderReader = await orders.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var orderIds = new List<long>();
        while (await orderReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            orderIds.Add(Convert.ToInt64(orderReader["order_id"], CultureInfo.InvariantCulture));
        }

        await orderReader.CloseAsync().ConfigureAwait(false);
        var log = "Return #" + returnId.ToString(CultureInfo.InvariantCulture) + " closed. Approved sum: "
                  + approvedSum.ToString("0.00", CultureInfo.InvariantCulture);
        var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var orderId in orderIds)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
                cancellationToken, orderId, time, adminUserId, log);
        }

        return ErpSimpleWriteResult.Ok("Return closed.", returnId);
    }

    private static async Task<long> StatusIdByCaptionsAsync(
        System.Data.Common.DbConnection connection,
        IReadOnlyList<string> captions,
        CancellationToken cancellationToken)
    {
        foreach (var caption in captions)
        {
            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1"),
                cancellationToken, caption);
            if (id > 0)
            {
                return id;
            }
        }

        return await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
    }
}
