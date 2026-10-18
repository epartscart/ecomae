using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Syncron-style inventory policy (owner-accepted enhancement from PR #8): global, category (item type) and item
/// policies; daily demand from stock movements; safety stock, reorder point and a per item × warehouse
/// recommendation; a 30-day forecast run; and a monthly service-level report.
/// </summary>
public static class ErpSyncronPolicy
{
    public static readonly IReadOnlyList<string> Scopes = ["global", "category", "item"];
    public static readonly IReadOnlyList<(string Method, string Label)> DemandMethods =
    [
        ("moving_avg", "Moving average"),
        ("exponential", "Exponential smoothing"),
        ("manual", "Manual (policy values only)"),
    ];

    public static readonly IReadOnlyList<string> ItemTypes = ["standard", "perishable", "serialized"];

    /// <summary>Syntetos–Boylan labels used on the recommended-order workbench.</summary>
    public static readonly IReadOnlyList<string> DemandTypes = ["Fast", "Erratic", "Slow", "Lumpy", "New"];

    public static readonly IReadOnlyList<string> OrderLineStatuses = ["pending", "confirmed", "rejected", "drafted"];

    public const int ForecastDays = 30;
    public const decimal DefaultServiceLevel = 95m;
    public const int DefaultLeadTimeDays = 7;
    public const int DefaultWindowDays = 90;
    public const int DefaultReviewDays = 30;
    public const decimal DefaultAlpha = 0.3m;

    public sealed record Policy(
        long Id,
        string Scope,
        string ScopeRef,
        string PolicyName,
        decimal SafetyStockQty,
        decimal ReorderPoint,
        decimal ReorderQty,
        decimal MaxStockQty,
        decimal ServiceLevelPct,
        int LeadTimeDays,
        int ReviewPeriodDays,
        string DemandMethod,
        int DemandWindowDays,
        decimal DemandAlpha);

    public sealed record Recommendation(
        long ItemId,
        string Sku,
        string ItemName,
        long WarehouseId,
        string WarehouseName,
        decimal OnHand,
        decimal DailyDemand,
        decimal SafetyStock,
        decimal ReorderPoint,
        decimal SuggestedOrderQty,
        int LeadTimeDays,
        decimal ServiceLevelTarget,
        string PolicyName,
        string Action,
        string DemandType = "New",
        decimal UnitCost = 0m,
        string ItemGroup = "",
        long SupplierId = 0,
        string SupplierName = "",
        string Status = "ok",
        string DueDate = "",
        decimal OrderQty = 0m,
        long PoId = 0,
        string ItemType = "");

    /// <summary>A saved decision for one item × warehouse on the recommended-order workbench.</summary>
    public sealed record OrderLine(
        long ItemId,
        long WarehouseId,
        long SupplierId,
        string SupplierName,
        decimal OrderQty,
        decimal UnitCost,
        string Status,
        string DueDate,
        long PoId);

    public sealed record ConfirmedOrderLine(
        long ItemId,
        long WarehouseId,
        long SupplierId,
        string SupplierName,
        decimal OrderQty,
        decimal UnitCost,
        string Sku,
        string ItemName,
        string WarehouseName);

    public sealed record ForecastRow(long ItemId, string Sku, long WarehouseId, string WarehouseName, string PeriodStart, string PeriodEnd, decimal ForecastQty, string Method);

    public sealed record ServiceLevelRow(long ItemId, string Sku, long WarehouseId, string WarehouseName, string PeriodMonth, decimal DemandQty, decimal FulfilledQty, int StockoutEvents, decimal ServiceLevel);

    /// <summary>One item × warehouse with its stock and its daily demand (oldest day first, one entry per calendar day).</summary>
    public sealed record ItemWarehouse(
        long ItemId,
        string Sku,
        string ItemName,
        string ItemType,
        long WarehouseId,
        string WarehouseName,
        decimal OnHand,
        IReadOnlyList<decimal> DailyDemand,
        string ItemGroup = "",
        long SupplierId = 0,
        string SupplierName = "",
        decimal UnitCost = 0m);

    public static decimal ServiceLevelZ(decimal pct) => pct switch
    {
        >= 99.9m => 3.09m,
        >= 99m => 2.33m,
        >= 98m => 2.05m,
        >= 97m => 1.88m,
        >= 96m => 1.75m,
        >= 95m => 1.65m,
        >= 90m => 1.28m,
        >= 85m => 1.04m,
        >= 80m => 0.84m,
        _ => 0.67m,
    };

    /// <summary>z × σ × √lead time, with σ taken as a quarter of the daily demand.</summary>
    public static decimal SafetyStock(decimal dailyDemand, int leadTimeDays, decimal z)
        => Round4(z * dailyDemand * 0.25m * (decimal)Math.Sqrt(Math.Max(leadTimeDays, 0)));

    public static decimal ReorderPoint(decimal dailyDemand, int leadTimeDays, decimal safetyStock)
        => Round4(dailyDemand * leadTimeDays + safetyStock);

    /// <summary>Average daily demand over the whole window, so quiet days count as zero.</summary>
    public static decimal MovingAverage(IReadOnlyList<decimal> daily, int windowDays)
    {
        var window = Math.Max(windowDays, 1);
        var take = daily.Skip(Math.Max(daily.Count - window, 0)).Sum();
        return Round4(take / window);
    }

    /// <summary>Exponential smoothing over the window's daily series, seeded with its first day.</summary>
    public static decimal Exponential(IReadOnlyList<decimal> daily, int windowDays, decimal alpha)
    {
        var series = daily.Skip(Math.Max(daily.Count - Math.Max(windowDays, 1), 0)).ToList();
        if (series.Count == 0)
        {
            return 0m;
        }

        var forecast = series[0];
        for (var i = 1; i < series.Count; i++)
        {
            forecast = alpha * series[i] + (1 - alpha) * forecast;
        }

        return Round4(forecast);
    }

    /// <summary>
    /// Classifies a daily series the way the recommended-order screen labels demand.
    /// Fast is smooth (ADI below 1.32 and CV² below 0.49), Erratic is variable and frequent,
    /// Slow is intermittent with a stable size, Lumpy is intermittent and variable.
    /// New when fewer than two days in the series have demand.
    /// </summary>
    public static string DemandType(IReadOnlyList<decimal>? daily)
    {
        if (daily is null || daily.Count < 2)
        {
            return "New";
        }

        var positives = new List<decimal>();
        foreach (var day in daily)
        {
            if (day > 0m)
            {
                positives.Add(day);
            }
        }

        if (positives.Count < 2)
        {
            return "New";
        }

        var adi = (decimal)daily.Count / positives.Count;
        var mean = positives.Average();
        if (mean <= 0m)
        {
            return "New";
        }

        var variance = positives.Sum(day => (day - mean) * (day - mean)) / positives.Count;
        var cv2 = variance / (mean * mean);
        var intermittent = adi >= 1.32m;
        var variable = cv2 >= 0.49m;
        return (intermittent, variable) switch
        {
            (false, false) => "Fast",
            (false, true) => "Erratic",
            (true, false) => "Slow",
            _ => "Lumpy",
        };
    }

    /// <summary>The item policy for the SKU, else the category policy for the item type, else the global policy.</summary>
    public static Policy? PolicyFor(IReadOnlyList<Policy> policies, string sku, string itemType)
        => policies.FirstOrDefault(p => p.Scope == "item" && string.Equals(p.ScopeRef, sku, StringComparison.OrdinalIgnoreCase))
           ?? policies.FirstOrDefault(p => p.Scope == "category" && string.Equals(p.ScopeRef, itemType, StringComparison.OrdinalIgnoreCase))
           ?? policies.FirstOrDefault(p => p.Scope == "global");

    public static decimal DailyDemandFor(ItemWarehouse row, Policy? policy)
    {
        var window = policy?.DemandWindowDays ?? DefaultWindowDays;
        return policy?.DemandMethod switch
        {
            "exponential" => Exponential(row.DailyDemand, window, policy.DemandAlpha),
            "manual" => 0m,
            _ => MovingAverage(row.DailyDemand, window),
        };
    }

    public static Recommendation Recommend(ItemWarehouse row, Policy? policy, DateOnly? today = null)
    {
        var lead = policy?.LeadTimeDays ?? DefaultLeadTimeDays;
        var service = policy?.ServiceLevelPct ?? DefaultServiceLevel;
        var review = policy?.ReviewPeriodDays ?? DefaultReviewDays;
        var daily = DailyDemandFor(row, policy);
        var safety = policy is { SafetyStockQty: > 0 } ? policy.SafetyStockQty : SafetyStock(daily, lead, ServiceLevelZ(service));
        var rop = policy is { ReorderPoint: > 0 } ? policy.ReorderPoint : ReorderPoint(daily, lead, safety);

        var action = "ok";
        if (row.OnHand <= 0 && daily > 0)
        {
            action = "stockout";
        }
        else if (rop > 0 && row.OnHand <= rop)
        {
            action = "reorder";
        }
        else if (policy is { MaxStockQty: > 0 } && row.OnHand > policy.MaxStockQty)
        {
            action = "overstock";
        }

        var suggested = 0m;
        if (action is "stockout" or "reorder")
        {
            suggested = policy is { ReorderQty: > 0 }
                ? policy.ReorderQty
                : Math.Ceiling(Math.Max(rop + daily * review - row.OnHand, 0m));
            if (policy is { MaxStockQty: > 0 } && row.OnHand + suggested > policy.MaxStockQty)
            {
                suggested = Math.Max(policy.MaxStockQty - row.OnHand, 0m);
            }
        }

        var status = action is "stockout" or "reorder" ? "pending" : "ok";
        var due = today is { } day ? day.AddDays(Math.Max(lead, 0)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        return new Recommendation(
            row.ItemId, row.Sku, row.ItemName, row.WarehouseId, row.WarehouseName, row.OnHand,
            daily, safety, rop, suggested, lead, service, policy?.PolicyName ?? "", action,
            DemandType(row.DailyDemand), row.UnitCost, row.ItemGroup, row.SupplierId, row.SupplierName,
            status, due, suggested, 0, row.ItemType);
    }

    /// <summary>Overlays a saved order-line decision onto a calculated recommendation.</summary>
    public static Recommendation ApplyOrderLine(Recommendation recommendation, OrderLine line)
    {
        var supplierId = line.SupplierId > 0 ? line.SupplierId : recommendation.SupplierId;
        var supplierName = line.SupplierName.Length > 0 ? line.SupplierName : recommendation.SupplierName;
        var unitCost = line.UnitCost > 0 ? line.UnitCost : recommendation.UnitCost;
        var due = line.DueDate.Length > 0 ? line.DueDate : recommendation.DueDate;
        var status = OrderLineStatuses.Contains(line.Status) ? line.Status : recommendation.Status;
        return recommendation with
        {
            SupplierId = supplierId,
            SupplierName = supplierName,
            UnitCost = unitCost,
            DueDate = due,
            Status = status,
            OrderQty = Round4(line.OrderQty),
            PoId = line.PoId,
        };
    }

    public static IReadOnlyList<(long ItemId, long WarehouseId, decimal Qty)> ParseLineGrid(string? grid)
    {
        var list = new List<(long, long, decimal)>();
        if (string.IsNullOrWhiteSpace(grid))
        {
            return list;
        }

        foreach (var raw in grid.Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':');
            if (parts.Length < 3
                || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId)
                || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warehouseId)
                || !decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var qty)
                || itemId <= 0
                || warehouseId <= 0)
            {
                continue;
            }

            if (qty < 0)
            {
                throw new ErpWriteException("Order quantity cannot be negative.");
            }

            list.Add((itemId, warehouseId, Round4(qty)));
        }

        return list;
    }

    public static IReadOnlyList<(long ItemId, long WarehouseId)> ParseLineKeys(string? keys)
    {
        var list = new List<(long, long)>();
        if (string.IsNullOrWhiteSpace(keys))
        {
            return list;
        }

        foreach (var raw in keys.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':');
            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId)
                && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warehouseId)
                && itemId > 0
                && warehouseId > 0
                && !list.Contains((itemId, warehouseId)))
            {
                list.Add((itemId, warehouseId));
            }
        }

        return list;
    }

    public static string Validate(Policy policy)
    {
        if (!Scopes.Contains(policy.Scope))
        {
            return "Scope must be global, category or item.";
        }

        if (policy.Scope == "item" && policy.ScopeRef.Trim().Length == 0)
        {
            return "Item policies need the item SKU.";
        }

        if (policy.Scope == "category" && !ItemTypes.Contains(policy.ScopeRef.Trim().ToLowerInvariant()))
        {
            return "Category policies need an item type: standard, perishable or serialized.";
        }

        if (policy.PolicyName.Trim().Length == 0)
        {
            return "Policy name is required.";
        }

        if (policy.SafetyStockQty < 0 || policy.ReorderPoint < 0 || policy.ReorderQty < 0 || policy.MaxStockQty < 0)
        {
            return "Quantities cannot be negative.";
        }

        if (policy.ServiceLevelPct is < 50m or > 99.99m)
        {
            return "Service level must be between 50 and 99.99 %.";
        }

        if (policy.LeadTimeDays is < 0 or > 365 || policy.ReviewPeriodDays is < 1 or > 365)
        {
            return "Lead time must be 0–365 days and the review period 1–365 days.";
        }

        if (policy.DemandWindowDays is < 7 or > 730)
        {
            return "Demand window must be 7–730 days.";
        }

        if (!DemandMethods.Any(m => m.Method == policy.DemandMethod))
        {
            return "Unknown demand method.";
        }

        if (policy.DemandAlpha is <= 0m or > 1m)
        {
            return "Smoothing alpha must be above 0 and at most 1.";
        }

        return "";
    }

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_inv_policies` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `scope` ENUM('global','category','item') NOT NULL DEFAULT 'global',
              `scope_ref` VARCHAR(120) NOT NULL DEFAULT '',
              `policy_name` VARCHAR(180) NOT NULL DEFAULT '',
              `safety_stock_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `reorder_point` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `reorder_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `max_stock_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `service_level_pct` DECIMAL(5,2) NOT NULL DEFAULT 95.00,
              `lead_time_days` SMALLINT UNSIGNED NOT NULL DEFAULT 7,
              `review_period_days` SMALLINT UNSIGNED NOT NULL DEFAULT 30,
              `demand_method` ENUM('moving_avg','exponential','manual') NOT NULL DEFAULT 'moving_avg',
              `demand_window_days` SMALLINT UNSIGNED NOT NULL DEFAULT 90,
              `demand_alpha` DECIMAL(4,3) NOT NULL DEFAULT 0.300,
              `active` TINYINT(1) NOT NULL DEFAULT 1,
              `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
              `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
              INDEX `idx_scope` (`scope`, `scope_ref`),
              INDEX `idx_active` (`active`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_inv_demand_forecast` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `item_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `warehouse_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `period_start` DATE NOT NULL,
              `period_end` DATE NOT NULL,
              `forecast_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `actual_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `variance_pct` DECIMAL(6,2) NOT NULL DEFAULT 0,
              `method` VARCHAR(40) NOT NULL DEFAULT 'moving_avg',
              `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
              INDEX `idx_item_wh` (`item_id`, `warehouse_id`),
              INDEX `idx_period` (`period_start`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_inv_service_levels` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `item_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `warehouse_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `period_month` CHAR(7) NOT NULL DEFAULT '',
              `demand_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `fulfilled_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `stockout_events` INT UNSIGNED NOT NULL DEFAULT 0,
              `service_level` DECIMAL(5,2) NOT NULL DEFAULT 0,
              `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
              UNIQUE KEY `uk_item_wh_month` (`item_id`, `warehouse_id`, `period_month`),
              INDEX `idx_period` (`period_month`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_inv_order_lines` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `item_id` INT UNSIGNED NOT NULL,
              `warehouse_id` INT UNSIGNED NOT NULL,
              `supplier_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `supplier_name` VARCHAR(180) NOT NULL DEFAULT '',
              `order_qty` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `unit_cost` DECIMAL(14,4) NOT NULL DEFAULT 0,
              `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
              `due_date` DATE NULL,
              `po_id` INT UNSIGNED NOT NULL DEFAULT 0,
              `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
              UNIQUE KEY `uk_item_wh` (`item_id`, `warehouse_id`),
              INDEX `idx_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<Policy>> ListPoliciesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<Policy>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `scope`, `scope_ref`, `policy_name`, `safety_stock_qty`, `reorder_point`, `reorder_qty`, `max_stock_qty`, `service_level_pct`,"
                              + " `lead_time_days`, `review_period_days`, `demand_method`, `demand_window_days`, `demand_alpha`"
                              + " FROM `epc_erp_inv_policies` WHERE `active` = 1 ORDER BY FIELD(`scope`, 'item', 'category', 'global'), `policy_name`, `id`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new Policy(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(10), CultureInfo.InvariantCulture),
                reader.GetString(11),
                Convert.ToInt32(reader.GetValue(12), CultureInfo.InvariantCulture),
                reader.GetDecimal(13)));
        }

        return list;
    }

    /// <summary>Inserts (id 0) or updates an active policy. Returns the id, or 0 when the id is not an active policy.</summary>
    public static async Task<long> SavePolicyAsync(DbConnection connection, Policy policy, CancellationToken cancellationToken)
    {
        var error = Validate(policy);
        if (error.Length > 0)
        {
            throw new ErpWriteException(error);
        }

        var scopeRef = policy.Scope switch
        {
            "global" => "",
            "category" => policy.ScopeRef.Trim().ToLowerInvariant(),
            _ => policy.ScopeRef.Trim(),
        };
        object?[] values =
        [
            policy.Scope, Clip(scopeRef, 120), Clip(policy.PolicyName.Trim(), 180),
            Round4(policy.SafetyStockQty), Round4(policy.ReorderPoint), Round4(policy.ReorderQty), Round4(policy.MaxStockQty),
            decimal.Round(policy.ServiceLevelPct, 2, MidpointRounding.AwayFromZero), policy.LeadTimeDays, policy.ReviewPeriodDays,
            policy.DemandMethod, policy.DemandWindowDays, decimal.Round(policy.DemandAlpha, 3, MidpointRounding.AwayFromZero),
        ];
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (policy.Id > 0)
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_inv_policies` SET `scope`=?, `scope_ref`=?, `policy_name`=?, `safety_stock_qty`=?, `reorder_point`=?, `reorder_qty`=?, `max_stock_qty`=?,"
                                 + " `service_level_pct`=?, `lead_time_days`=?, `review_period_days`=?, `demand_method`=?, `demand_window_days`=?, `demand_alpha`=?, `updated_at`=?"
                                 + " WHERE `id`=? AND `active`=1"),
                cancellationToken,
                [.. values, now, policy.Id]).ConfigureAwait(false);
            if (updated == 0 && await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_inv_policies` WHERE `id`=? AND `active`=1"), cancellationToken, policy.Id).ConfigureAwait(false) == 0)
            {
                return 0;
            }

            return policy.Id;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_inv_policies` (`scope`,`scope_ref`,`policy_name`,`safety_stock_qty`,`reorder_point`,`reorder_qty`,`max_stock_qty`,"
                             + "`service_level_pct`,`lead_time_days`,`review_period_days`,`demand_method`,`demand_window_days`,`demand_alpha`,`active`,`created_at`,`updated_at`)"
                             + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,1,?,?)"),
            cancellationToken,
            [.. values, now, now]).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<bool> DeactivatePolicyAsync(DbConnection connection, long id, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_inv_policies` SET `active` = 0, `updated_at` = ? WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id).ConfigureAwait(false) > 0;

    /// <summary>
    /// Active items × active warehouses that hold a stock row or had demand in the last <paramref name="windowDays"/> days.
    /// Demand is <c>sale_out</c> quantity plus negative adjustments, per calendar day up to yesterday.
    /// </summary>
    public static async Task<IReadOnlyList<ItemWarehouse>> LoadItemWarehousesAsync(
        DbConnection connection,
        int windowDays,
        long warehouseId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        windowDays = Math.Clamp(windowDays, 1, 730);
        var from = today.AddDays(-windowDays);
        var fromUnix = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var toUnix = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var whFilter = warehouseId > 0 ? " AND w.`id` = @p2" : "";

        var demand = new Dictionary<(long, long), decimal[]>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT m.`item_id`, m.`warehouse_id`, FLOOR((m.`movement_date` - @p0) / 86400) AS d, SUM(ABS(m.`qty`))"
                                  + " FROM `epc_erp_inv_movements` m"
                                  + " WHERE m.`active` = 1 AND m.`movement_date` >= @p0 AND m.`movement_date` < @p1"
                                  + " AND (m.`movement_type` = 'sale_out' OR (m.`movement_type` = 'adjustment' AND m.`qty` < 0))"
                                  + (warehouseId > 0 ? " AND m.`warehouse_id` = @p2" : "")
                                  + " GROUP BY m.`item_id`, m.`warehouse_id`, d";
            ErpDb.AddParameters(command, fromUnix, toUnix, warehouseId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = (Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture));
                var day = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
                if (!demand.TryGetValue(key, out var series))
                {
                    demand[key] = series = new decimal[windowDays];
                }

                if (day >= 0 && day < windowDays)
                {
                    series[day] += Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
                }
            }
        }

        var rows = new List<ItemWarehouse>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT i.`id`, i.`sku`, i.`name`, i.`item_type`, w.`id`, w.`name`, COALESCE(SUM(s.`qty_on_hand`), 0), COUNT(s.`id`)"
                                  + " FROM `epc_erp_inv_items` i CROSS JOIN `epc_erp_inv_warehouses` w"
                                  + " LEFT JOIN `epc_erp_inv_stock` s ON s.`item_id` = i.`id` AND s.`warehouse_id` = w.`id`"
                                  + " WHERE i.`active` = 1 AND w.`active` = 1" + whFilter
                                  + " GROUP BY i.`id`, i.`sku`, i.`name`, i.`item_type`, w.`id`, w.`name`"
                                  + " ORDER BY i.`sku`, w.`name`";
            ErpDb.AddParameters(command, fromUnix, toUnix, warehouseId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var itemId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var whId = Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture);
                var hasStockRow = Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture) > 0;
                var hasDemand = demand.TryGetValue((itemId, whId), out var series);
                if (!hasStockRow && !hasDemand)
                {
                    continue;
                }

                rows.Add(new ItemWarehouse(
                    itemId,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    whId,
                    reader.GetString(5),
                    Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    series ?? new decimal[windowDays]));
            }
        }

        return rows;
    }

    public static async Task<IReadOnlyList<Recommendation>> RecommendAsync(DbConnection connection, long warehouseId, DateOnly today, CancellationToken cancellationToken)
        => await RecommendAsync(connection, await ListPoliciesAsync(connection, cancellationToken).ConfigureAwait(false), warehouseId, today, cancellationToken).ConfigureAwait(false);

    public static async Task<IReadOnlyList<Recommendation>> RecommendAsync(DbConnection connection, IReadOnlyList<Policy> policies, long warehouseId, DateOnly today, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureOrderLineColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = await LoadItemWarehousesAsync(connection, WindowFor(policies), warehouseId, today, cancellationToken).ConfigureAwait(false);
        var extras = await LoadItemExtrasAsync(connection, warehouseId, cancellationToken).ConfigureAwait(false);
        var lines = await LoadOrderLinesAsync(connection, warehouseId, cancellationToken).ConfigureAwait(false);
        var list = new List<Recommendation>(rows.Count);
        foreach (var row in rows)
        {
            var enriched = extras.TryGetValue((row.ItemId, row.WarehouseId), out var extra)
                ? row with { ItemGroup = extra.ItemGroup, SupplierId = extra.SupplierId, SupplierName = extra.SupplierName, UnitCost = extra.UnitCost }
                : row;
            var recommendation = Recommend(enriched, PolicyFor(policies, enriched.Sku, enriched.ItemType), today);
            if (lines.TryGetValue((row.ItemId, row.WarehouseId), out var line))
            {
                recommendation = ApplyOrderLine(recommendation, line);
            }

            list.Add(recommendation);
        }

        return list;
    }

    public static async Task<IReadOnlyList<(long Id, string Name)>> WarehousesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<(long, string)>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `name` FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `name`, `id`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
        }

        return list;
    }

    public static async Task<IReadOnlyList<(long Id, string Name)>> SuppliersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_erp_suppliers", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var list = new List<(long, string)>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `name` FROM `epc_erp_suppliers` WHERE `active` = 1 ORDER BY `name`, `id`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
        }

        return list;
    }

    /// <summary>Inserts or updates the order quantity. An empty status keeps the saved status and defaults a new row to pending.</summary>
    public static async Task UpsertOrderLineAsync(
        DbConnection connection,
        long itemId,
        long warehouseId,
        decimal orderQty,
        long supplierId,
        string supplierName,
        decimal unitCost,
        string? status,
        string? dueDate,
        CancellationToken cancellationToken)
    {
        if (itemId <= 0 || warehouseId <= 0)
        {
            throw new ErpWriteException("Item and warehouse are required.");
        }

        if (orderQty < 0 || unitCost < 0)
        {
            throw new ErpWriteException("Order quantity cannot be negative.");
        }

        var changeStatus = status is not null && OrderLineStatuses.Contains(status);
        var storedStatus = changeStatus ? status! : "pending";
        string? due = null;
        if (!string.IsNullOrWhiteSpace(dueDate))
        {
            if (!DateOnly.TryParseExact(dueDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                throw new ErpWriteException("Due date must be YYYY-MM-DD.");
            }

            due = dueDate.Trim();
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_erp_inv_order_lines`
                  (`item_id`,`warehouse_id`,`supplier_id`,`supplier_name`,`order_qty`,`unit_cost`,`status`,`due_date`,`po_id`,`updated_at`)
                VALUES (?,?,?,?,?,?,?,?,0,?)
                ON DUPLICATE KEY UPDATE
                  `order_qty` = VALUES(`order_qty`),
                  `supplier_id` = IF(VALUES(`supplier_id`) > 0, VALUES(`supplier_id`), `supplier_id`),
                  `supplier_name` = IF(VALUES(`supplier_name`) <> '', VALUES(`supplier_name`), `supplier_name`),
                  `unit_cost` = IF(VALUES(`unit_cost`) > 0, VALUES(`unit_cost`), `unit_cost`),
                  `due_date` = IFNULL(VALUES(`due_date`), `due_date`),
                  `status` = IF(? = 1, VALUES(`status`), `status`),
                  `updated_at` = VALUES(`updated_at`)
                """),
            cancellationToken,
            itemId,
            warehouseId,
            supplierId,
            Clip(supplierName.Trim(), 180),
            Round4(orderQty),
            Round4(unitCost),
            storedStatus,
            due,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            changeStatus ? 1 : 0).ConfigureAwait(false);
    }

    public static async Task<int> SetOrderLineStatusAsync(
        DbConnection connection,
        IReadOnlyList<(long ItemId, long WarehouseId)> keys,
        string status,
        IReadOnlyList<string> fromStatuses,
        CancellationToken cancellationToken)
    {
        if (!OrderLineStatuses.Contains(status) || fromStatuses.Count == 0)
        {
            return 0;
        }

        var placeholders = string.Join(",", fromStatuses.Select((_, i) => "?"));
        var changed = 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (itemId, warehouseId) in keys)
        {
            object?[] args = [status, now, itemId, warehouseId, .. fromStatuses.Cast<object?>()];
            changed += await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_inv_order_lines` SET `status` = ?, `updated_at` = ? WHERE `item_id` = ? AND `warehouse_id` = ? AND `status` IN (" + placeholders + ")"),
                cancellationToken,
                args).ConfigureAwait(false);
        }

        return changed;
    }

    public static async Task<int> SetAllOrderLineStatusAsync(
        DbConnection connection,
        string status,
        string fromStatus,
        CancellationToken cancellationToken)
    {
        if (!OrderLineStatuses.Contains(status) || !OrderLineStatuses.Contains(fromStatus))
        {
            return 0;
        }

        return await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_inv_order_lines` SET `status` = ?, `updated_at` = ? WHERE `status` = ?"),
            cancellationToken,
            status, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), fromStatus).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<ConfirmedOrderLine>> ConfirmedOrderLinesAsync(
        DbConnection connection,
        IReadOnlyList<(long ItemId, long WarehouseId)>? onlyKeys,
        CancellationToken cancellationToken)
    {
        var list = new List<ConfirmedOrderLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT l.`item_id`, l.`warehouse_id`, l.`supplier_id`, l.`supplier_name`, l.`order_qty`, l.`unit_cost`,"
                              + " COALESCE(i.`sku`, ''), COALESCE(i.`name`, ''), COALESCE(w.`name`, '')"
                              + " FROM `epc_erp_inv_order_lines` l"
                              + " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = l.`item_id`"
                              + " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = l.`warehouse_id`"
                              + " WHERE l.`status` = 'confirmed' AND l.`order_qty` > 0"
                              + " ORDER BY l.`supplier_id`, i.`sku`, w.`name`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var warehouseId = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            if (onlyKeys is { Count: > 0 } && !onlyKeys.Contains((itemId, warehouseId)))
            {
                continue;
            }

            list.Add(new ConfirmedOrderLine(
                itemId,
                warehouseId,
                Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8)));
        }

        return list;
    }

    public static async Task StampDraftedAsync(
        DbConnection connection,
        long itemId,
        long warehouseId,
        long purchaseOrderId,
        CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_inv_order_lines` SET `status` = 'drafted', `po_id` = ?, `updated_at` = ? WHERE `item_id` = ? AND `warehouse_id` = ? AND `status` = 'confirmed'"),
            cancellationToken,
            purchaseOrderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), itemId, warehouseId).ConfigureAwait(false);

    public static async Task<(long Id, string Sku, string Name, string ItemType)?> FindItemAsync(
        DbConnection connection,
        long itemId,
        string sku,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (itemId > 0)
        {
            command.CommandText = "SELECT `id`, `sku`, `name`, `item_type` FROM `epc_erp_inv_items` WHERE `active` = 1 AND `id` = @p0 LIMIT 1";
            ErpDb.AddParameters(command, itemId);
        }
        else
        {
            var trimmed = sku.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            command.CommandText = "SELECT `id`, `sku`, `name`, `item_type` FROM `epc_erp_inv_items` WHERE `active` = 1 AND `sku` = @p0 LIMIT 1";
            ErpDb.AddParameters(command, trimmed);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    public static async Task<bool> WarehouseExistsAsync(DbConnection connection, long warehouseId, CancellationToken cancellationToken)
        => warehouseId > 0 && await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_inv_warehouses` WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            warehouseId).ConfigureAwait(false) > 0;

    public static async Task<string> SupplierNameAsync(DbConnection connection, long supplierId, CancellationToken cancellationToken)
    {
        if (supplierId <= 0 || !await TableExistsAsync(connection, "epc_erp_suppliers", cancellationToken).ConfigureAwait(false))
        {
            return "";
        }

        return await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `name` FROM `epc_erp_suppliers` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            supplierId).ConfigureAwait(false) ?? "";
    }

    private static int WindowFor(IReadOnlyList<Policy> policies)
        => policies.Count == 0 ? DefaultWindowDays : Math.Max(policies.Max(p => p.DemandWindowDays), DefaultWindowDays);

    private static async Task EnsureOrderLineColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpLazySchema.AddColumnIfMissingAsync(connection, "epc_erp_inv_items", "item_group", "varchar(64) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.AddColumnIfMissingAsync(connection, "epc_erp_inv_items", "default_vendor_id", "int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.AddColumnIfMissingAsync(connection, "epc_erp_inv_items", "purchase_price", "decimal(14,4) NOT NULL DEFAULT 0.0000", cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.AddColumnIfMissingAsync(connection, "epc_erp_inv_stock", "avg_unit_cost", "decimal(14,4) NOT NULL DEFAULT 0.0000", cancellationToken).ConfigureAwait(false);
    }

    private sealed record ItemExtra(string ItemGroup, long SupplierId, string SupplierName, decimal UnitCost);

    private static async Task<Dictionary<(long ItemId, long WarehouseId), ItemExtra>> LoadItemExtrasAsync(
        DbConnection connection,
        long warehouseId,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<(long, long), ItemExtra>();
        var hasGroup = await ColumnExistsAsync(connection, "epc_erp_inv_items", "item_group", cancellationToken).ConfigureAwait(false);
        var hasVendor = await ColumnExistsAsync(connection, "epc_erp_inv_items", "default_vendor_id", cancellationToken).ConfigureAwait(false);
        var hasPrice = await ColumnExistsAsync(connection, "epc_erp_inv_items", "purchase_price", cancellationToken).ConfigureAwait(false);
        var hasCost = await ColumnExistsAsync(connection, "epc_erp_inv_stock", "avg_unit_cost", cancellationToken).ConfigureAwait(false);
        if (!hasGroup && !hasVendor && !hasPrice && !hasCost)
        {
            return map;
        }

        var names = new Dictionary<long, string>();
        if (hasVendor && await TableExistsAsync(connection, "epc_erp_suppliers", cancellationToken).ConfigureAwait(false))
        {
            await using var suppliers = connection.CreateCommand();
            suppliers.CommandText = "SELECT `id`, `name` FROM `epc_erp_suppliers`";
            await using var supplierReader = await suppliers.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await supplierReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                names[Convert.ToInt64(supplierReader.GetValue(0), CultureInfo.InvariantCulture)] = supplierReader.GetString(1);
            }
        }

        await using var command = connection.CreateCommand();
        var whFilter = warehouseId > 0 ? " AND w.`id` = @p0" : "";
        var costSql = hasCost
            ? "(SELECT COALESCE(AVG(st.`avg_unit_cost`), 0) FROM `epc_erp_inv_stock` st WHERE st.`item_id` = i.`id` AND st.`warehouse_id` = w.`id`)"
            : "0";
        command.CommandText = "SELECT i.`id`, w.`id`, "
                              + (hasGroup ? "IFNULL(i.`item_group`, '')" : "''") + ", "
                              + (hasVendor ? "IFNULL(i.`default_vendor_id`, 0)" : "0") + ", "
                              + (hasPrice ? "IFNULL(i.`purchase_price`, 0)" : "0") + ", "
                              + costSql
                              + " FROM `epc_erp_inv_items` i CROSS JOIN `epc_erp_inv_warehouses` w"
                              + " WHERE i.`active` = 1 AND w.`active` = 1" + whFilter;
        if (warehouseId > 0)
        {
            ErpDb.AddParameters(command, warehouseId);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var whId = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            var supplierId = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
            var price = Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
            var average = Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture);
            names.TryGetValue(supplierId, out var supplierName);
            map[(itemId, whId)] = new ItemExtra(
                reader.GetString(2),
                supplierId,
                supplierName ?? "",
                average > 0 ? average : price);
        }

        return map;
    }

    private static async Task<Dictionary<(long, long), OrderLine>> LoadOrderLinesAsync(
        DbConnection connection,
        long warehouseId,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<(long, long), OrderLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `item_id`, `warehouse_id`, `supplier_id`, `supplier_name`, `order_qty`, `unit_cost`, `status`,"
                              + " IFNULL(DATE_FORMAT(`due_date`, '%Y-%m-%d'), ''), `po_id` FROM `epc_erp_inv_order_lines`"
                              + (warehouseId > 0 ? " WHERE `warehouse_id` = @p0" : "");
        if (warehouseId > 0)
        {
            ErpDb.AddParameters(command, warehouseId);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var whId = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            map[(itemId, whId)] = new OrderLine(
                itemId,
                whId,
                Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetString(6),
                reader.GetString(7),
                Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture));
        }

        return map;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table, column).ConfigureAwait(false) > 0;

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false) > 0;

    /// <summary>Writes the next-30-day forecast per item × warehouse, replacing any forecast already written today.</summary>
    public static async Task<int> RunForecastAsync(DbConnection connection, long warehouseId, DateOnly today, CancellationToken cancellationToken)
    {
        var policies = await ListPoliciesAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = await LoadItemWarehousesAsync(connection, WindowFor(policies), warehouseId, today, cancellationToken).ConfigureAwait(false);
        var start = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = today.AddDays(ForecastDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            var policy = PolicyFor(policies, row.Sku, row.ItemType);
            var method = policy?.DemandMethod ?? "moving_avg";
            var qty = Round4(DailyDemandFor(row, policy) * ForecastDays);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("DELETE FROM `epc_erp_inv_demand_forecast` WHERE `item_id` = ? AND `warehouse_id` = ? AND `period_start` = ?"),
                cancellationToken,
                row.ItemId, row.WarehouseId, start).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("INSERT INTO `epc_erp_inv_demand_forecast` (`item_id`,`warehouse_id`,`period_start`,`period_end`,`forecast_qty`,`method`,`created_at`) VALUES (?,?,?,?,?,?,?)"),
                cancellationToken,
                row.ItemId, row.WarehouseId, start, end, qty, method, now).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    public static async Task<IReadOnlyList<ForecastRow>> LatestForecastAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        var list = new List<ForecastRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT f.`item_id`, COALESCE(i.`sku`, ''), f.`warehouse_id`, COALESCE(w.`name`, ''), DATE_FORMAT(f.`period_start`, '%Y-%m-%d'), DATE_FORMAT(f.`period_end`, '%Y-%m-%d'), f.`forecast_qty`, f.`method`"
                              + " FROM `epc_erp_inv_demand_forecast` f"
                              + " JOIN (SELECT MAX(`period_start`) AS ps FROM `epc_erp_inv_demand_forecast`) latest ON latest.ps = f.`period_start`"
                              + " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = f.`item_id` LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = f.`warehouse_id`"
                              + " ORDER BY f.`forecast_qty` DESC, i.`sku` LIMIT @p0";
        ErpDb.AddParameters(command, Math.Clamp(limit, 1, 1000));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ForecastRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetDecimal(6),
                reader.GetString(7)));
        }

        return list;
    }

    /// <summary>Adds a month's demand, fulfilled quantity and stockouts for an item × warehouse and recomputes its fill rate.</summary>
    public static async Task RecordServiceLevelAsync(
        DbConnection connection,
        long itemId,
        long warehouseId,
        string periodMonth,
        decimal demandQty,
        decimal fulfilledQty,
        int stockoutEvents,
        CancellationToken cancellationToken)
    {
        if (itemId <= 0 || warehouseId <= 0)
        {
            throw new ErpWriteException("Item and warehouse are required.");
        }

        if (!DateOnly.TryParseExact(periodMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new ErpWriteException("Month must be YYYY-MM.");
        }

        if (demandQty < 0 || fulfilledQty < 0 || stockoutEvents < 0)
        {
            throw new ErpWriteException("Quantities cannot be negative.");
        }

        if (fulfilledQty > demandQty)
        {
            throw new ErpWriteException("Fulfilled quantity cannot exceed demand.");
        }

        var level = demandQty > 0 ? decimal.Round(fulfilledQty / demandQty * 100m, 2, MidpointRounding.AwayFromZero) : 100m;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_inv_service_levels` (`item_id`,`warehouse_id`,`period_month`,`demand_qty`,`fulfilled_qty`,`stockout_events`,`service_level`,`created_at`) VALUES (?,?,?,?,?,?,?,?)"
                             + " ON DUPLICATE KEY UPDATE `service_level` = IF(`demand_qty` + VALUES(`demand_qty`) > 0, ROUND((`fulfilled_qty` + VALUES(`fulfilled_qty`)) / (`demand_qty` + VALUES(`demand_qty`)) * 100, 2), 100),"
                             + " `demand_qty` = `demand_qty` + VALUES(`demand_qty`), `fulfilled_qty` = `fulfilled_qty` + VALUES(`fulfilled_qty`), `stockout_events` = `stockout_events` + VALUES(`stockout_events`)"),
            cancellationToken,
            itemId, warehouseId, periodMonth, Round4(demandQty), Round4(fulfilledQty), stockoutEvents, level, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<ServiceLevelRow>> ServiceLevelReportAsync(DbConnection connection, string fromMonth, string toMonth, CancellationToken cancellationToken)
    {
        var list = new List<ServiceLevelRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT s.`item_id`, COALESCE(i.`sku`, ''), s.`warehouse_id`, COALESCE(w.`name`, ''), s.`period_month`, s.`demand_qty`, s.`fulfilled_qty`, s.`stockout_events`, s.`service_level`"
                              + " FROM `epc_erp_inv_service_levels` s LEFT JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id`"
                              + " WHERE s.`period_month` >= @p0 AND s.`period_month` <= @p1 ORDER BY s.`period_month` DESC, s.`item_id` ASC LIMIT 500";
        ErpDb.AddParameters(command, fromMonth, toMonth);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ServiceLevelRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture),
                reader.GetDecimal(8)));
        }

        return list;
    }

    private static decimal Round4(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
