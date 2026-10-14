using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Sales profitability performance: posted sales revenue against average inventory cost.
/// The same rows feed the in-ERP analytical workspace and the Power BI dataset.
/// </summary>
public static class ErpSalesProfitability
{
    public const string Tab = "sm_performance";
    public const string CostTab = "cm_sales_profitability";
    public const string DatasetId = "sales-profitability";

    public static readonly string[] Headers =
        ["Date", "Customer", "Item", "Description", "Qty", "Revenue", "Unit cost", "Cost", "Margin", "Margin %"];

    public sealed record Line(
        string Date,
        string Customer,
        string ItemCode,
        string Description,
        decimal Qty,
        decimal Revenue,
        decimal UnitCost,
        decimal Cost,
        decimal Margin,
        decimal MarginPct);

    public sealed record Group(string Key, string Label, decimal Qty, decimal Revenue, decimal Cost, decimal Margin, decimal MarginPct);

    public sealed record Report(
        decimal Revenue,
        decimal Cost,
        decimal Margin,
        decimal MarginPct,
        int LineCount,
        IReadOnlyList<Group> ByCustomer,
        IReadOnlyList<Group> ByItem,
        IReadOnlyList<Group> ByMonth,
        IReadOnlyList<Line> Lines,
        string Note);

    public static bool IsProfitabilityTab(string? tab)
        => string.Equals(tab, Tab, StringComparison.OrdinalIgnoreCase)
           || string.Equals(tab, CostTab, StringComparison.OrdinalIgnoreCase);

    public static decimal MarginPercent(decimal revenue, decimal margin)
        => revenue == 0m ? 0m : Round2(margin / revenue * 100m);

    public static Line Make(string date, string customer, string itemCode, string description, decimal qty, decimal revenue, decimal unitCost)
    {
        var revenueRounded = Round2(revenue);
        var cost = Round2(qty * unitCost);
        var margin = Round2(revenueRounded - cost);
        return new(
            date,
            customer.Trim(),
            itemCode.Trim(),
            description.Trim(),
            Round4(qty),
            revenueRounded,
            Round4(unitCost),
            cost,
            margin,
            MarginPercent(revenueRounded, margin));
    }

    public static IReadOnlyList<Line> Filter(IReadOnlyList<Line> lines, string? customer, string? item)
    {
        var customerText = (customer ?? string.Empty).Trim();
        var itemText = (item ?? string.Empty).Trim();
        return lines.Where(line =>
            (customerText.Length == 0 || line.Customer.Contains(customerText, StringComparison.OrdinalIgnoreCase))
            && (itemText.Length == 0
                || line.ItemCode.Contains(itemText, StringComparison.OrdinalIgnoreCase)
                || line.Description.Contains(itemText, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public static Report Summarize(IReadOnlyList<Line> lines, string note = "")
    {
        var revenue = Round2(lines.Sum(line => line.Revenue));
        var cost = Round2(lines.Sum(line => line.Cost));
        var margin = Round2(revenue - cost);
        return new(
            revenue,
            cost,
            margin,
            MarginPercent(revenue, margin),
            lines.Count,
            GroupBy(lines, line => line.Customer, line => line.Customer),
            GroupBy(lines, line => line.ItemCode.Length > 0 ? line.ItemCode : line.Description, line => line.ItemCode.Length > 0 ? line.ItemCode + " " + line.Description : line.Description),
            GroupBy(lines, line => line.Date.Length >= 7 ? line.Date[..7] : line.Date, line => line.Date.Length >= 7 ? line.Date[..7] : line.Date),
            lines,
            note);
    }

    public static async Task<Report> LoadAsync(
        DbConnection connection,
        DateOnly from,
        DateOnly to,
        string? customer,
        string? item,
        CancellationToken cancellationToken,
        string? country = null)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var raw = new List<(long When, long CustomerId, string SoNo, string Item, string Description, decimal Qty, decimal Revenue)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT so.`time_created`, so.`customer_user_id`, so.`so_no`,
                       l.`item_code`, l.`description`, l.`qty`, l.`line_ex_vat`
                FROM `epc_erp_sales_orders` so
                INNER JOIN `epc_erp_sales_order_lines` l ON l.`sales_order_id` = so.`id`
                WHERE so.`status` IN ('confirmed','invoiced')
                  AND so.`time_created` >= @p0 AND so.`time_created` < @p1
                ORDER BY so.`time_created` DESC, so.`id` DESC, l.`line_no`
                LIMIT 5000
                """;
            ErpDb.AddParameters(command, start, end);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture)));
            }
        }

        var note = "Posted sales that are confirmed or invoiced. Cost is quantity times the item's average on-hand unit cost. Draft and cancelled orders are left out.";
        if (!string.IsNullOrWhiteSpace(country))
        {
            var allowed = await CustomerIdsForCountryAsync(
                connection,
                raw.Select(row => row.CustomerId).Distinct().Where(id => id > 0).ToList(),
                country,
                cancellationToken).ConfigureAwait(false);
            var label = ErpVoiceInterpreter.CountryLabel(ErpVoiceInterpreter.CountryCode(country));
            if (label.Length == 0)
            {
                label = country.Trim();
            }

            if (allowed is null)
            {
                note += " Customer country is not on file yet, so this report includes every country in the period.";
            }
            else
            {
                raw = raw.Where(row => allowed.Contains(row.CustomerId)).ToList();
                note += " Limited to customers in " + label + ".";
            }
        }

        var names = await CustomerNamesAsync(connection, raw.Select(row => row.CustomerId).Distinct().Where(id => id > 0).ToList(), cancellationToken).ConfigureAwait(false);
        var costs = await UnitCostsAsync(connection, cancellationToken).ConfigureAwait(false);
        var lines = new List<Line>(raw.Count);
        foreach (var row in raw)
        {
            var when = row.When > 0
                ? DateTimeOffset.FromUnixTimeSeconds(row.When).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "";
            names.TryGetValue(row.CustomerId, out var name);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = row.CustomerId > 0 ? "Customer " + row.CustomerId.ToString(CultureInfo.InvariantCulture) : row.SoNo;
            }

            costs.TryGetValue(row.Item.Trim(), out var unitCost);
            lines.Add(Make(when, name, row.Item, row.Description, row.Qty, row.Revenue, unitCost));
        }

        return Summarize(Filter(lines, customer, item), note);
    }

    private static async Task<HashSet<long>?> CustomerIdsForCountryAsync(
        DbConnection connection,
        IReadOnlyList<long> ids,
        string country,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        if (!await TableExistsAsync(connection, "users_profiles", cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var allowed = new HashSet<long>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `user_id`, `data_value` FROM `users_profiles` WHERE `data_key` = 'epc_reg_country' AND `user_id` IN ("
            + string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")";
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var stored = reader.IsDBNull(1) ? "" : reader.GetString(1);
                if (ErpVoiceInterpreter.CountryMatches(stored, country))
                {
                    allowed.Add(id);
                }
            }
        }
        catch (DbException)
        {
            return null;
        }

        return allowed;
    }

    private static List<Group> GroupBy(IReadOnlyList<Line> lines, Func<Line, string> key, Func<Line, string> label)
    {
        return lines
            .GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var revenue = Round2(group.Sum(line => line.Revenue));
                var cost = Round2(group.Sum(line => line.Cost));
                var margin = Round2(revenue - cost);
                var caption = group.Select(label).FirstOrDefault(text => text.Trim().Length > 0) ?? group.Key;
                return new Group(group.Key, caption.Trim(), Round4(group.Sum(line => line.Qty)), revenue, cost, margin, MarginPercent(revenue, margin));
            })
            .OrderByDescending(group => group.Revenue)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToList();
    }

    private static async Task<Dictionary<long, string>> CustomerNamesAsync(DbConnection connection, IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, string>();
        if (ids.Count == 0 || !await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false))
        {
            return map;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `user_id`, `email` FROM `users` WHERE `user_id` IN (" + string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")";
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
            }
        }
        catch (DbException)
        {
            return [];
        }

        return map;
    }

    private static async Task<Dictionary<string, decimal>> UnitCostsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (!await TableExistsAsync(connection, "epc_erp_inv_items", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_erp_inv_stock", cancellationToken).ConfigureAwait(false))
        {
            return map;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.`sku`, COALESCE(AVG(s.`avg_unit_cost`), 0)
            FROM `epc_erp_inv_items` i
            LEFT JOIN `epc_erp_inv_stock` s ON s.`item_id` = i.`id`
            WHERE i.`sku` <> ''
            GROUP BY i.`sku`
            """;
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map[reader.GetString(0)] = Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException)
        {
            return [];
        }

        return map;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false) > 0;

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round4(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
