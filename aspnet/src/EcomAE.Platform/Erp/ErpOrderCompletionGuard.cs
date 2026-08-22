using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Port of PHP <c>epc_erp_order_is_complete</c> / <c>epc_erp_assert_order_complete</c>
/// (<c>content/shop/finance/epc_erp_helpers.php</c>): ERP postings may only reference a shop
/// order once the order (or every counted line) sits in a finish status.
/// </summary>
internal static class ErpOrderCompletionGuard
{
    public static string Message(long orderId, string context)
        => context + " requires order #" + orderId.ToString(CultureInfo.InvariantCulture)
            + " to be in Completed status (all lines finished in CP).";

    public static async Task AssertCompleteAsync(
        DbConnection connection,
        long orderId,
        string context,
        CancellationToken cancellationToken)
    {
        if (!await IsCompleteAsync(connection, orderId, cancellationToken).ConfigureAwait(false))
        {
            throw new ErpWriteException(Message(orderId, context));
        }
    }

    public static async Task<bool> IsCompleteAsync(
        DbConnection connection,
        long orderId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (orderId <= 0)
        {
            return false;
        }

        var orderFinish = await IdsAsync(
            connection,
            "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC",
            cancellationToken).ConfigureAwait(false);
        var itemFinish = await IdsAsync(
            connection,
            "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC",
            cancellationToken).ConfigureAwait(false);

        if (orderFinish.Count > 0 && await HasOrderStatusColumnAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            var found = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 AND `status` IN ("
                    + Join(orderFinish) + ") LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
            return found > 0;
        }

        if (itemFinish.Count == 0)
        {
            return false;
        }

        var notCounted = await IdsAsync(
            connection,
            "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0",
            cancellationToken).ConfigureAwait(false);
        var exclusion = string.Concat(notCounted.Select(id => " AND `status` != " + id.ToString(CultureInfo.InvariantCulture)));

        var totalItems = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ?" + exclusion),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (totalItems <= 0)
        {
            return false;
        }

        var openItems = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ?" + exclusion
                + " AND `status` NOT IN (" + Join(itemFinish) + ")"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        return openItems == 0;
    }

    /// <summary>PHP joins the status ids straight into SQL after an int cast; these are ids read from the DB.</summary>
    public static string Join(IReadOnlyList<long> ids)
        => ids.Count == 0 ? "0" : string.Join(',', ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

    private static async Task<List<long>> IdsAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
            // PHP swallows missing status-reference tables and falls back to "not complete".
        }

        return ids;
    }

    private static async Task<bool> HasOrderStatusColumnAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var column = await ErpDb.StringAsync(
                connection,
                null,
                "SHOW COLUMNS FROM `shop_orders` LIKE 'status'",
                cancellationToken).ConfigureAwait(false);
            return !string.IsNullOrEmpty(column);
        }
        catch (DbException)
        {
            return false;
        }
    }
}
