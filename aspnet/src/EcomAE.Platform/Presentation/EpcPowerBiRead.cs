using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Read-only Power BI dataset queries. Missing tables surface as <see cref="DbException"/>
/// so the caller can return PHP's dataset catch. This type does not create schema and does not insert rows.
/// </summary>
public static class EpcPowerBiRead
{
    public static async Task<EpcPowerBiDatasets.Dataset> OrdersAsync(
        DbConnection connection,
        string siteKey,
        int limit,
        CancellationToken cancellationToken)
    {
        var hasStatus = await HasColumnAsync(connection, "shop_orders", "status", cancellationToken).ConfigureAwait(false);
        var statusSql = hasStatus
            ? "(SELECT `name` FROM `shop_orders_statuses_ref` WHERE `id` = `shop_orders`.`status` LIMIT 1)"
            : "(SELECT `name` FROM `shop_orders_items_statuses_ref` WHERE `id` = (SELECT `status` FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` ORDER BY `id` DESC LIMIT 1) LIMIT 1)";
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `id`, `time`, `user_id`, `paid`, `paid_type`, " + statusSql + " AS status_name"
            + " FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var unix = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            rows.Add(
            [
                siteKey,
                reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                unix > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+00:00"
                    : "",
                reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                !reader.IsDBNull(3) && Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0 ? 1 : 0,
                reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                reader.IsDBNull(5) ? "" : reader.GetString(5),
            ]);
        }

        return EpcPowerBiDatasets.OrdersReady(siteKey, rows, limit);
    }

    public static async Task<EpcPowerBiDatasets.Dataset> SalesAsync(
        DbConnection connection,
        long fromUnix,
        long toUnix,
        CancellationToken cancellationToken)
    {
        var cmp = await CompletionAsync(connection, cancellationToken).ConfigureAwait(false);
        var sums = ErpDashboardReadService.OrderSumSql(cmp);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `shop_orders`.`id`, `shop_orders`.`time`, `shop_orders`.`user_id`, "
            + sums.Paid + " AS paid_amount, `shop_orders`.`how_get_json`, "
            + cmp.OrderCompleteExpr + " AS order_complete, "
            + "(SELECT `email` FROM `users` WHERE `users`.`user_id` = `shop_orders`.`user_id` LIMIT 1) AS customer_email "
            + "FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ? "
            + "ORDER BY `time` DESC LIMIT 5000");
        ErpDb.AddParameters(command, fromUnix, toUnix);
        var raw = new List<(long Id, long Time, long UserId, decimal Paid, string HowGet, bool Complete, string Email)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var complete = !reader.IsDBNull(5) && Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0;
                raw.Add((
                    reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? "",
                    complete,
                    reader.IsDBNull(6) ? "" : reader.GetString(6)));
            }
        }

        var tax = await ErpDashboardReadService.LoadTenantVatAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = new List<IReadOnlyList<object?>>();
        var ctxCache = new Dictionary<long, ErpDashboardReadService.CustomerVatContext>();
        foreach (var order in raw)
        {
            if (!order.Complete || order.Id <= 0)
            {
                continue;
            }

            if (!ctxCache.TryGetValue(order.UserId, out var ctx))
            {
                ctx = await ErpDashboardReadService.CustomerContextAsync(connection, order.UserId, cancellationToken).ConfigureAwait(false);
                ctxCache[order.UserId] = ctx;
            }

            var items = await ErpDashboardReadService.ItemsAsync(connection, order.Id, cancellationToken).ConfigureAwait(false);
            var dest = ErpDashboardReadService.DestinationCountry(order.HowGet, ctx.Country);
            var exports = dest != "AE";
            var goodsRate = ErpDashboardReadService.SupplyRate(ctx.Country, exports, tax.RatePercent);
            var inclusive = ErpDashboardReadService.DisplayMode(ctx.VatType) == "inclusive" && goodsRate > 0m;
            var lineNet = 0m;
            var goodsGross = 0m;
            foreach (var (unit, qty) in items)
            {
                var line = ErpDashboardReadService.LineAmounts(unit, qty, goodsRate, inclusive, tax.SalesEnabled);
                lineNet += line.LineNet;
                goodsGross += line.Gross;
            }

            var courierNet = ErpDashboardReadService.Round2(Math.Max(0m, ErpDashboardReadService.CourierAmount(order.HowGet)));
            var courierRate = ErpDashboardReadService.SupplyRate(dest, exports, tax.RatePercent);
            var courierGross = courierNet;
            if (courierNet > 0m && courierRate > 0m && tax.SalesEnabled)
            {
                courierGross = ErpDashboardReadService.Round2(courierNet + ErpDashboardReadService.Round2(courierNet * courierRate / 100m));
            }

            var dueBase = ErpDashboardReadService.Round2(ErpDashboardReadService.Round2(goodsGross) + ErpDashboardReadService.Round2(courierGross));
            var due = Math.Max(0m, ErpDashboardReadService.Round2(dueBase - ErpDashboardReadService.Round2(order.Paid)));
            var when = order.Time > 0
                ? DateTimeOffset.FromUnixTimeSeconds(order.Time).ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "";
            rows.Add(
            [
                order.Id,
                when,
                order.Email,
                EpcPowerBiDatasets.Money(ErpDashboardReadService.Round2(lineNet)),
                EpcPowerBiDatasets.Money(ErpDashboardReadService.Round2(order.Paid)),
                EpcPowerBiDatasets.Money(due),
            ]);
        }

        return EpcPowerBiDatasets.ReportReady("sales", fromUnix, toUnix, EpcPowerBiDatasets.SalesHeaders, rows);
    }

    public static async Task<EpcPowerBiDatasets.Dataset> StockAsync(
        DbConnection connection,
        long fromUnix,
        long toUnix,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.`sku`, i.`name`, w.`name`, s.`qty_on_hand`, s.`avg_unit_cost`
            FROM `epc_erp_inv_stock` s
            INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id`
            INNER JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id`
            WHERE i.`active` = 1
            ORDER BY w.`name`, i.`sku`, s.`batch_no`
            """;
        var rows = new List<IReadOnlyList<object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var qty = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
            var avg = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
            rows.Add(
            [
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                EpcPowerBiDatasets.Money(qty),
                EpcPowerBiDatasets.Money(avg),
                EpcPowerBiDatasets.Money(ErpDashboardReadService.Round2(qty * avg)),
            ]);
        }

        return EpcPowerBiDatasets.ReportReady("stock", fromUnix, toUnix, EpcPowerBiDatasets.StockHeaders, rows);
    }

    public static async Task<EpcPowerBiDatasets.Dataset> GlAsync(
        DbConnection connection,
        long fromUnix,
        long toUnix,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT a.`code`, a.`name`, a.`account_type`, a.`normal_side`, a.`opening_balance`,
                   IFNULL(x.debits, 0), IFNULL(x.credits, 0)
            FROM `epc_erp_coa_accounts` a
            LEFT JOIN (
                SELECT l.`coa_id` AS coa_id,
                       IFNULL(SUM(l.`debit`), 0) AS debits,
                       IFNULL(SUM(l.`credit`), 0) AS credits
                FROM `epc_erp_gl_lines` l
                INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id`
                WHERE j.`active` = 1 AND j.`journal_date` <= ?
                GROUP BY l.`coa_id`
            ) x ON x.`coa_id` = a.`id`
            WHERE a.`active` = 1
            ORDER BY a.`code` ASC
            """);
        ErpDb.AddParameters(command, toUnix > 0 ? toUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var rows = new List<IReadOnlyList<object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var opening = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
            var debits = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture);
            var credits = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture);
            var side = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var balance = side == "credit" ? opening + credits - debits : opening + debits - credits;
            if (Math.Abs(balance) < 0.005m)
            {
                continue;
            }

            decimal dr;
            decimal cr;
            if (side == "debit" && balance < 0)
            {
                dr = 0m;
                cr = Math.Abs(balance);
            }
            else if (side == "credit" && balance < 0)
            {
                dr = Math.Abs(balance);
                cr = 0m;
            }
            else if (balance > 0 && side == "debit")
            {
                dr = balance;
                cr = 0m;
            }
            else if (balance > 0 && side == "credit")
            {
                dr = 0m;
                cr = balance;
            }
            else
            {
                dr = 0m;
                cr = 0m;
            }

            rows.Add(
            [
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                EpcPowerBiDatasets.Money(dr),
                EpcPowerBiDatasets.Money(cr),
                EpcPowerBiDatasets.Money(balance),
            ]);
        }

        return EpcPowerBiDatasets.ReportReady("gl", fromUnix, toUnix, EpcPowerBiDatasets.GlHeaders, rows);
    }

    public static async Task<EpcPowerBiDatasets.Dataset> MetricsAsync(
        DbConnection platform,
        string siteKey,
        CancellationToken cancellationToken)
    {
        await using var command = platform.CreateCommand();
        command.CommandText = """
            SELECT s.`metric_key`, s.`value`, s.`previous_value`, s.`change_pct`, s.`period_start`, s.`computed_at`
            FROM `epc_bi_snapshots` s
            INNER JOIN (
                SELECT `metric_key`, MAX(`period_start`) AS max_date
                FROM `epc_bi_snapshots`
                WHERE `site_key` = @key
                GROUP BY `metric_key`
            ) latest ON s.`metric_key` = latest.`metric_key` AND s.`period_start` = latest.max_date
            WHERE s.`site_key` = @key2
            """;
        Add(command, "@key", siteKey);
        Add(command, "@key2", siteKey);
        var rows = new List<IReadOnlyList<object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(
            [
                siteKey,
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                EpcPowerBiDatasets.Money(reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)),
                EpcPowerBiDatasets.Money(reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture)),
                EpcPowerBiDatasets.Money(reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture)),
                reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? "",
                reader.IsDBNull(5) ? "" : Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? "",
            ]);
        }

        return EpcPowerBiDatasets.MetricsReady(rows);
    }

    private static async Task<ErpCompletionSql> CompletionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var orderFinish = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
        var itemFinish = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
        var notCount = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0", cancellationToken).ConfigureAwait(false);
        var hasStatus = await HasColumnAsync(connection, "shop_orders", "status", cancellationToken).ConfigureAwait(false);
        return ErpDashboardReadService.CompletionSql(hasStatus, orderFinish, itemFinish, notCount);
    }

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
        }

        return ids;
    }

    private static async Task<bool> HasColumnAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW COLUMNS FROM `" + table + "` LIKE '" + column + "'";
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is not null && value is not DBNull;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
