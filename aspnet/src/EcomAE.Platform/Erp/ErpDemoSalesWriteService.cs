using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_demo_seed_sales</c> / <c>epc_demo_clear_sales</c> twins
/// (<c>epc_erp_demo_sales.php</c>). Completed demo sales orders tagged
/// <c>email_not_auth='DEMO-SALE'</c> for the executive dashboard KPIs.
/// Note: PHP uses mt_srand(20260601) + mt_rand; this port keeps the same seed constant and
/// the same draw pattern but uses .NET <see cref="Random"/>, so generated demo rows are
/// deterministic yet not byte-identical to PHP's sequence.
/// </summary>
public interface IErpDemoSalesWriteService
{
    Task<ErpDemoSeedResult> SeedAsync(int months, CancellationToken cancellationToken = default);

    /// <returns>number of demo orders removed (PHP epc_demo_clear_sales return value)</returns>
    Task<ErpDemoClearResult> ClearAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpDemoSeedResult(bool Ok, string Message, long Orders, long Lines, decimal Revenue, int Writes);
public sealed record ErpDemoClearResult(bool Ok, string Message, long Cleared, int Writes);

public sealed class ErpDemoSalesWriteService : IErpDemoSalesWriteService
{
    public const int DefaultMonths = 6;
    private const int PhpSeed = 20260601;
    private const string DemoTag = "DEMO-SALE";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpDemoSalesWriteService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ErpDemoSeedResult> SeedAsync(int months, CancellationToken cancellationToken = default)
    {
        if (months <= 0) months = DefaultMonths;
        if (!_connections.IsConfigured)
            return new ErpDemoSeedResult(false, "TenantRegistry DB is not configured.", 0, 0, 0m, 0);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var orderStatus = await ScalarLongAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
        var itemStatus = await ScalarLongAsync(connection, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 AND `count_flag` != 0 ORDER BY `order` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
        if (itemStatus <= 0)
        {
            itemStatus = await ScalarLongAsync(connection, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
        }
        if (orderStatus <= 0 || itemStatus <= 0)
        {
            return new ErpDemoSeedResult(false, "No finished-order status configured in this store", 0, 0, 0m, 0);
        }

        var cleared = await ClearCoreAsync(connection, cancellationToken).ConfigureAwait(false);

        var officeId = await ScalarLongAsync(connection, "SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
        var storageId = await ScalarLongAsync(connection, "SELECT `id` FROM `shop_storages` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);

        // PHP epc_erp_inventory_stock_report($db, 0) — stock rows joined to active items.
        var items = new List<DemoItem>();
        var seen = new Dictionary<long, int>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT s.`item_id`, s.`avg_unit_cost`, i.`sku`, i.`name` FROM `epc_erp_inv_stock` s"
                + " INNER JOIN `epc_erp_inv_items` i ON i.id = s.item_id"
                + " INNER JOIN `epc_erp_inv_warehouses` w ON w.id = s.warehouse_id"
                + " WHERE i.active = 1 ORDER BY w.name, i.sku, s.batch_no";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var cost = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                if (cost <= 0m) continue;
                var itemId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var item = new DemoItem(
                    cost,
                    reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty);
                if (seen.TryGetValue(itemId, out var index))
                    items[index] = item; // PHP array-key overwrite keeps last row's value
                else
                {
                    seen[itemId] = items.Count;
                    items.Add(item);
                }
            }
        }
        if (items.Count == 0)
        {
            return new ErpDemoSeedResult(false, "No costed inventory items to base sample sales on", 0, 0, 0m, 0);
        }

        var random = new Random(PhpSeed);
        long orderCount = 0;
        long lineCount = 0;
        var revenue = 0m;
        var localNow = _clock.GetLocalNow();

        for (var m = months - 1; m >= 0; m--)
        {
            // PHP strtotime('-' . $m . ' months') then date('Y')/date('n') in local time.
            var monthDate = localNow.DateTime.AddMonths(-m);
            var year = monthDate.Year;
            var mon = monthDate.Month;
            // a gentle upward trend toward recent months (PHP round = away from zero)
            var ordersThisMonth = 8 + (int)Math.Round((months - m) * 1.5, MidpointRounding.AwayFromZero) + random.Next(0, 5);
            for (var o = 0; o < ordersThisMonth; o++)
            {
                var day = Math.Min(28, 1 + random.Next(0, 28));
                var ts = new DateTimeOffset(new DateTime(year, mon, day, random.Next(9, 19), random.Next(0, 60), 0, DateTimeKind.Unspecified), localNow.Offset).ToUnixTimeSeconds();
                var orderId = await InsertOrderAsync(connection, ts, orderStatus, officeId, cancellationToken).ConfigureAwait(false);
                var nLines = 1 + random.Next(0, 3);
                for (var l = 0; l < nLines; l++)
                {
                    var it = items[random.Next(0, items.Count)];
                    var markup = 1.2m + random.Next(0, 51) / 100m;
                    var price = Math.Round(it.Cost * markup, 2, MidpointRounding.AwayFromZero);
                    var qty = 1 + random.Next(0, 6);
                    var article = it.Sku.Length > 0 ? it.Sku : "ITEM-" + l.ToString(CultureInfo.InvariantCulture);
                    var name = it.Name.Length > 0 ? it.Name : article;
                    if (name.Length > 255) name = name[..255];
                    await InsertItemAsync(connection, orderId, price, qty, itemStatus, article, name, Math.Round(it.Cost, 2, MidpointRounding.AwayFromZero), officeId, storageId, cancellationToken).ConfigureAwait(false);
                    lineCount++;
                    revenue += price * qty;
                }
                orderCount++;
            }
        }

        var message = "Seeded " + orderCount.ToString(CultureInfo.InvariantCulture)
            + " sample orders (" + lineCount.ToString(CultureInfo.InvariantCulture)
            + " lines, " + Math.Round(revenue, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture)
            + " AED revenue)";
        return new ErpDemoSeedResult(true, message, orderCount, lineCount, Math.Round(revenue, 2, MidpointRounding.AwayFromZero), (int)(orderCount + lineCount + cleared));
    }

    public async Task<ErpDemoClearResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
            return new ErpDemoClearResult(false, "TenantRegistry DB is not configured.", 0, 0);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var cleared = await ClearCoreAsync(connection, cancellationToken).ConfigureAwait(false);
        return new ErpDemoClearResult(true, "Cleared " + cleared.ToString(CultureInfo.InvariantCulture) + " sample orders", cleared, (int)cleared);
    }

    /// <summary>PHP epc_demo_clear_sales: remove DEMO-SALE orders and their details/items/logs.</summary>
    private static async Task<long> ClearCoreAsync(DbConnection connection, CancellationToken ct)
    {
        var ids = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id` FROM `shop_orders` WHERE `email_not_auth` = '" + DemoTag + "'";
            try
            {
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
            catch (DbException)
            {
                // shop_orders missing — PHP's deletes would also fail silently
                return 0;
            }
        }
        if (ids.Count == 0) return 0;

        var inList = string.Join(",", ids);
        foreach (var sql in new[]
                 {
                     "DELETE FROM `shop_orders_items_details` WHERE `order_id` IN (" + inList + ")",
                     "DELETE FROM `shop_orders_items` WHERE `order_id` IN (" + inList + ")",
                     "DELETE FROM `shop_orders_logs` WHERE `order_id` IN (" + inList + ")",
                     "DELETE FROM `shop_orders` WHERE `id` IN (" + inList + ")",
                 })
        {
            try
            {
                await ErpDb.ExecuteAsync(connection, null, sql, ct).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // PHP: table may not exist in some deployments; continue
            }
        }
        return ids.Count;
    }

    private static async Task<long> InsertOrderAsync(DbConnection connection, long time, long status, long officeId, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders` (`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`, `office_id`) VALUES (0, 0, ?, 1, ?, 1, 1, '{}', '', '" + DemoTag + "', ?)"),
            ct,
            time,
            status,
            officeId).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, ct).ConfigureAwait(false);
    }

    private static async Task InsertItemAsync(DbConnection connection, long orderId, decimal price, int qty, long status, string article, string name, decimal cost, long officeId, long storageId, CancellationToken ct)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_orders_items`"
                + " (`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,"
                + " `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`, `t2_time_to_exe`, `t2_time_to_exe_guaranteed`,"
                + " `t2_storage`, `t2_min_order`, `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`, `sao_state`, `sao_robot`, `t2_json_params`)"
                + " VALUES (?, 2, ?, ?, 0, ?, 'DEMO', ?, ?, ?, 10, 1, 1, '', 1, 100, 0, ?, ?, ?, 0, 0, '')"),
            ct,
            orderId,
            price,
            qty,
            status,
            article,
            article,
            name,
            cost,
            officeId,
            storageId).ConfigureAwait(false);

    private static async Task<long> ScalarLongAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        try
        {
            return await ErpDb.LongAsync(connection, null, ErpDb.Positional(sql), ct).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private sealed record DemoItem(decimal Cost, string Sku, string Name);
}
