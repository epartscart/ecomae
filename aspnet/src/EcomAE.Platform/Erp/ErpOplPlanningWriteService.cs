using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP epc_erp_order_planning ajax twins: <c>opl_seed_demo</c>
/// (epc_opl_seed_demo_demand), <c>opl_clear_demo</c> (epc_opl_clear_demo_demand)
/// and <c>opl_confirm_all</c> (epc_opl_confirm_all — pending due recommendations
/// marked confirmed via epc_opl_set_status upsert). Ports the PHP planning engine
/// (demand series, ADI/CV² classification, Z-factor, reorder-point compute) needed
/// by confirm-all. PHP seeds the mt_rand RNG per item×warehouse; C# uses a seeded
/// Random — the shape of the generated demand is identical but exact values differ.
/// </summary>
public interface IErpOplPlanningWriteService
{
    Task<ErpOplSeedResult> SeedDemoAsync(long warehouseId, CancellationToken cancellationToken = default);
    Task<ErpOplClearResult> ClearDemoAsync(CancellationToken cancellationToken = default);
    Task<ErpOplConfirmAllResult> ConfirmAllAsync(long warehouseId, CancellationToken cancellationToken = default);
    Task<ErpOplCreatePosResult> CreateDraftPosAsync(long warehouseId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpOplAutoplanResult> AutoplanAsync(long warehouseId, int adminId, CancellationToken cancellationToken = default);
}

public sealed record ErpOplSeedResult(bool Ok, string Message, int Items, int Movements, int Writes);
public sealed record ErpOplClearResult(bool Ok, string Message, long Cleared, int Writes);
public sealed record ErpOplConfirmAllResult(bool Ok, string Message, int Confirmed, int Writes);
public sealed record ErpOplCreatePosResult(bool Ok, string Message, int Pos, int Lines, decimal Value, int Assign, int Writes);
public sealed record ErpOplAutoplanResult(bool Ok, string Message, int Confirmed, ErpOplCreatePosResult Pos, int Writes);

public sealed class ErpOplPlanningWriteService : IErpOplPlanningWriteService
{
    private const int DemandMonths = 12;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPurchaseOrderWriteService _purchaseOrders;
    private readonly TimeProvider _clock;

    public ErpOplPlanningWriteService(IErpWriteConnectionFactory connections, IErpPurchaseOrderWriteService purchaseOrders, TimeProvider? clock = null)
    {
        _connections = connections;
        _purchaseOrders = purchaseOrders;
        _clock = clock ?? TimeProvider.System;
    }

    private sealed record StockRow(long ItemId, long WarehouseId, string Sku, string Name, string Unit, string WarehouseName, double QtyOnHand, double AvgUnitCost);
    private sealed record Params(long LeadTimeDays, double TargetServiceLevel, long ReviewPeriodDays, double MinOrderQty, double OrderMultiple, double ManualBuffer, string Supplier);

    public async Task<ErpOplSeedResult> SeedDemoAsync(long warehouseId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", 0, 0, 0);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var writes = 0;

        writes += await ErpDb.ExecuteAsync(connection, null,
            "DELETE FROM `epc_erp_inv_movements` WHERE `reference`='DEMO-DEMAND'", cancellationToken).ConfigureAwait(false);

        var stock = await StockReportAsync(connection, warehouseId, cancellationToken).ConfigureAwait(false);
        var pos = new Dictionary<string, (long ItemId, long WarehouseId, double OnHand, double Cost)>(StringComparer.Ordinal);
        foreach (var s in stock)
        {
            var key = s.ItemId + ":" + s.WarehouseId;
            if (!pos.TryGetValue(key, out var p))
            {
                p = (s.ItemId, s.WarehouseId, 0.0, s.AvgUnitCost);
            }
            p.OnHand += s.QtyOnHand;
            if (s.AvgUnitCost > 0) p.Cost = s.AvgUnitCost;
            pos[key] = p;
        }

        var patterns = new[] { "smooth", "erratic", "intermittent", "lumpy" };
        var itemCount = 0;
        var moveCount = 0;
        var idx = 0;
        var now = _clock.GetUtcNow();
        foreach (var p in pos.Values)
        {
            idx++;
            var rng = new Random((int)(p.ItemId * 7919 + p.WarehouseId));
            var onHand = Math.Max(1.0, p.OnHand);
            var cost = p.Cost > 0 ? p.Cost : 1.0;
            var pattern = patterns[idx % 4];
            var needsOrder = idx % 2 == 0;
            var baseDemand = needsOrder
                ? onHand * (0.7 + NextRand(rng, 0, 60) / 100.0)
                : onHand * (0.08 + NextRand(rng, 0, 18) / 100.0);
            baseDemand = Math.Max(1.0, baseDemand);

            for (var i = DemandMonths - 1; i >= 0; i--)
            {
                var monthTs = now.AddMonths(-i);
                var qty = pattern switch
                {
                    "smooth" => baseDemand * (0.85 + NextRand(rng, 0, 30) / 100.0),
                    "erratic" => baseDemand * (0.3 + NextRand(rng, 0, 170) / 100.0),
                    "intermittent" => NextRand(rng, 0, 100) < 55 ? baseDemand * (0.8 + NextRand(rng, 0, 60) / 100.0) : 0.0,
                    _ => NextRand(rng, 0, 100) < 40 ? baseDemand * (1.0 + NextRand(rng, 0, 250) / 100.0) : 0.0,
                };
                qty = Math.Round(qty, qty < 5 ? 2 : 0);
                if (qty <= 0) continue;
                var dt = new DateTimeOffset(monthTs.Year, monthTs.Month, Math.Min(28, 10 + rng.Next(16)), 12, 0, 0, TimeSpan.Zero);
                writes += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                    "INSERT INTO `epc_erp_inv_movements` (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`reference`,`note`,`movement_date`,`active`) VALUES ('sale_out',?,?,?,?,?,'DEMO-DEMAND','Seeded demand',?,1)"),
                    cancellationToken,
                    p.WarehouseId, p.ItemId, qty, cost, Math.Round(qty * cost, 2), dt.ToUnixTimeSeconds()).ConfigureAwait(false);
                moveCount++;
            }
            itemCount++;
        }

        return new(true, $"Seeded {moveCount} demand movements across {itemCount} items", itemCount, moveCount, writes);
    }

    public async Task<ErpOplClearResult> ClearDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", 0, 0);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        // PHP epc_opl_clear_demo_demand does not ensure schema; DELETEs fail closed.
        await ErpDb.ExecuteAsync(connection, null, "DELETE FROM `epc_erp_order_recommendations`", cancellationToken).ConfigureAwait(false);
        var cleared = await ErpDb.ExecuteAsync(connection, null,
            "DELETE FROM `epc_erp_inv_movements` WHERE `reference`='DEMO-DEMAND'", cancellationToken).ConfigureAwait(false);
        return new(true, $"Cleared {cleared} seeded demand movements", cleared, cleared + 1);
    }

    public async Task<ErpOplConfirmAllResult> ConfirmAllAsync(long warehouseId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", 0, 0);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var recs = await RecommendationsAsync(connection, warehouseId, onlyDue: true, statusFilter: "pending", search: "", cancellationToken).ConfigureAwait(false);
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        var writes = 0;
        foreach (var r in recs)
        {
            // PHP epc_opl_set_status upsert (status=confirmed).
            writes += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "INSERT INTO `epc_erp_order_recommendations` (`item_id`,`warehouse_id`,`roq`,`order_value`,`status`,`supplier`,`time_updated`) VALUES (?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `roq`=VALUES(`roq`), `order_value`=VALUES(`order_value`), `status`=VALUES(`status`), `supplier`=VALUES(`supplier`), `time_updated`=VALUES(`time_updated`)"),
                cancellationToken,
                r.ItemId, r.WarehouseId, r.Roq, r.Value, "confirmed", r.Supplier, now).ConfigureAwait(false);
        }
        return new(true, $"{recs.Count} recommendation(s) confirmed", recs.Count, writes);
    }

    public async Task<ErpOplAutoplanResult> AutoplanAsync(long warehouseId, int adminId, CancellationToken cancellationToken = default)
    {
        var confirmed = await ConfirmAllAsync(warehouseId, cancellationToken).ConfigureAwait(false);
        var pos = await CreateDraftPosAsync(warehouseId, adminId, cancellationToken).ConfigureAwait(false);
        var message = $"Confirmed {confirmed.Confirmed} due line(s). {pos.Message}";
        return new(pos.Pos > 0, message, confirmed.Confirmed, pos, confirmed.Writes + pos.Writes);
    }

    /// <summary>PHP epc_opl_create_draft_pos: group due confirmed recommendations by supplier into draft POs.</summary>
    public async Task<ErpOplCreatePosResult> CreateDraftPosAsync(long warehouseId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", 0, 0, 0m, 0, 0);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var supById = new Dictionary<long, string>();
        var supByName = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `name` FROM `epc_erp_suppliers` WHERE `active` = 1 ORDER BY `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var sid = Convert.ToInt64(reader.GetValue(0));
                var name = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
                supById[sid] = name;
                supByName[name.Trim().ToLowerInvariant()] = sid;
            }
        }
        if (supById.Count == 0)
        {
            return new(false, "No suppliers defined yet — add a supplier first, then raise POs.", 0, 0, 0m, 0, 0);
        }
        var fallbackSid = supById.Keys.OrderBy(k => k).First();

        var orderedMap = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `item_id`, `warehouse_id`, `ordered_po_id` FROM `epc_erp_order_recommendations`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (Convert.ToInt64(reader.GetValue(2)) > 0)
                {
                    orderedMap.Add(Convert.ToInt64(reader.GetValue(0)) + ":" + Convert.ToInt64(reader.GetValue(1)));
                }
            }
        }

        var recs = await RecommendationsAsync(connection, warehouseId, onlyDue: false, statusFilter: "confirmed", search: "", cancellationToken).ConfigureAwait(false);
        var groups = new Dictionary<long, List<Recommendation>>();
        var assignCount = 0;
        foreach (var r in recs)
        {
            if (r.Roq <= 0) continue;
            var key = r.ItemId + ":" + r.WarehouseId;
            if (orderedMap.Contains(key)) continue;
            var supName = r.Supplier.Trim().ToLowerInvariant();
            long sid;
            if (supName.Length > 0 && supByName.TryGetValue(supName, out var match))
            {
                sid = match;
            }
            else
            {
                sid = fallbackSid;
                assignCount++;
            }
            if (!groups.TryGetValue(sid, out var lines)) { lines = []; groups[sid] = lines; }
            lines.Add(r);
        }

        var posCreated = 0;
        var linesTotal = 0;
        var valueTotal = 0.0;
        var writes = 0;
        var today = _clock.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var (sid, lines) in groups)
        {
            var hasUnassigned = false;
            var sumEx = 0.0;
            var noteLines = new List<string>();
            foreach (var r in lines)
            {
                var supName = r.Supplier.Trim().ToLowerInvariant();
                if (supName.Length == 0 || !supByName.ContainsKey(supName)) hasUnassigned = true;
                sumEx += r.Value;
                var label = r.Sku.Length > 0 ? r.Sku : "#" + r.ItemId;
                noteLines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1} — qty {2} @ {3} = {4}{5}",
                    label,
                    r.Name,
                    r.Roq.ToString("0.###", CultureInfo.InvariantCulture),
                    r.UnitCost.ToString("0.00", CultureInfo.InvariantCulture),
                    r.Value.ToString("0.00", CultureInfo.InvariantCulture),
                    r.WarehouseName.Length > 0 ? " [" + r.WarehouseName + "]" : ""));
            }
            var supLabel = supById.TryGetValue(sid, out var n) ? n : "Supplier #" + sid;
            var title = hasUnassigned && groups.Count == 1 && sid == fallbackSid
                ? "Replenishment " + today + " (supplier to assign)"
                : "Replenishment " + today + " — " + supLabel;
            var notes = "Auto-drafted from Order planning confirmed recommendations.\n";
            if (hasUnassigned) notes += "NOTE: some lines had no supplier set on the item worksheet — please verify/assign before sending.\n";
            notes += "\n" + string.Join("\n", noteLines);

            var saved = await _purchaseOrders.SaveAsync(
                new ErpPurchaseOrderInput
                {
                    SupplierId = (int)sid,
                    Title = title,
                    AmountExVat = (decimal)Math.Round(sumEx, 2),
                    Status = "draft",
                    Notes = notes,
                },
                adminId,
                cancellationToken).ConfigureAwait(false);
            if (saved.Id > 0)
            {
                posCreated++;
                foreach (var r in lines)
                {
                    writes += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                        "UPDATE `epc_erp_order_recommendations` SET `ordered_po_id` = ? WHERE `item_id` = ? AND `warehouse_id` = ?"),
                        cancellationToken, saved.Id, r.ItemId, r.WarehouseId).ConfigureAwait(false);
                    linesTotal++;
                    valueTotal += r.Value;
                }
            }
        }

        if (posCreated == 0)
        {
            return new(false, "No new confirmed lines to order — confirm recommendations first (or they are already on a draft PO).", 0, 0, 0m, 0, 0);
        }
        var msg = string.Format(
            CultureInfo.InvariantCulture,
            "Created {0} draft PO{1} covering {2} line{3} ({4} AED). Review them in Purchasing → Purchase orders before sending.",
            posCreated, posCreated == 1 ? "" : "s", linesTotal, linesTotal == 1 ? "" : "s",
            valueTotal.ToString("0.00", CultureInfo.InvariantCulture));
        if (assignCount > 0)
        {
            msg += string.Format(CultureInfo.InvariantCulture,
                " {0} line{1} had no supplier set — grouped into a \"supplier to assign\" draft.", assignCount, assignCount == 1 ? "" : "s");
        }
        return new(true, msg, posCreated, linesTotal, (decimal)Math.Round(valueTotal, 2), assignCount, writes);
    }

    private sealed record Recommendation(long ItemId, long WarehouseId, double Roq, double Value, string Supplier, string Status, string Sku, string Name, string WarehouseName, double UnitCost);

    private async Task<List<Recommendation>> RecommendationsAsync(DbConnection connection, long warehouseId, bool onlyDue, string statusFilter, string search, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var stock = await StockReportAsync(connection, warehouseId, cancellationToken).ConfigureAwait(false);

        var agg = new Dictionary<string, (StockRow Row, double QtyOnHand, double CostQty, double Cost)>(StringComparer.Ordinal);
        foreach (var s in stock)
        {
            var key = s.ItemId + ":" + s.WarehouseId;
            if (!agg.TryGetValue(key, out var a))
            {
                a = (s, 0.0, 0.0, 0.0);
            }
            a.QtyOnHand += s.QtyOnHand;
            a.CostQty += s.QtyOnHand;
            a.Cost += s.QtyOnHand * s.AvgUnitCost;
            agg[key] = a;
        }

        var statusMap = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_id, warehouse_id, status FROM `epc_erp_order_recommendations`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                statusMap[Convert.ToInt64(reader.GetValue(0)) + ":" + Convert.ToInt64(reader.GetValue(1))] = Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "";
            }
        }

        var needle = search.ToLowerInvariant();
        var outList = new List<(string Key, Recommendation Rec)>();
        foreach (var (key, a) in agg)
        {
            var s = a.Row;
            var avgCost = a.CostQty > 0 ? a.Cost / a.CostQty : s.AvgUnitCost;
            var row = s with { QtyOnHand = a.QtyOnHand, AvgUnitCost = avgCost };
            if (needle.Length > 0 && !(s.Sku + " " + s.Name).ToLowerInvariant().Contains(needle, StringComparison.Ordinal)) continue;
            var m = await ComputeAsync(connection, row, cancellationToken).ConfigureAwait(false);
            var status = statusMap.TryGetValue(key, out var st) ? st : "pending";
            if (onlyDue && m.Roq <= 0) continue;
            if (statusFilter.Length > 0 && status != statusFilter) continue;
            outList.Add((key, new Recommendation(m.ItemId, m.WarehouseId, m.Roq, m.Value, m.Supplier, status, s.Sku, s.Name, s.WarehouseName, row.AvgUnitCost)));
        }
        outList.Sort((x, y) =>
        {
            var xd = x.Rec.Roq > 0;
            var yd = y.Rec.Roq > 0;
            if (xd != yd) return xd ? -1 : 1;
            return y.Rec.Value.CompareTo(x.Rec.Value);
        });
        return outList.Select(t => t.Rec).ToList();
    }

    private sealed record Metrics(long ItemId, long WarehouseId, double Roq, double Value, string Supplier);

    private async Task<Metrics> ComputeAsync(DbConnection connection, StockRow stockRow, CancellationToken cancellationToken)
    {
        var itemId = stockRow.ItemId;
        var warehouseId = stockRow.WarehouseId;
        var p = await ParamsGetAsync(connection, itemId, warehouseId, cancellationToken).ConfigureAwait(false);
        var series = await DemandSeriesAsync(connection, itemId, warehouseId, DemandMonths, cancellationToken).ConfigureAwait(false);
        var cls = ClassifyDemand(series);

        var months = series.Count;
        var totalDemand = series.Sum();
        var monthlyMean = months > 0 ? totalDemand / months : 0.0;
        var add = monthlyMean / 30.4375;

        var variance = 0.0;
        foreach (var v in series) variance += (v - monthlyMean) * (v - monthlyMean);
        variance = months > 0 ? variance / months : 0.0;
        var sigmaMonthly = Math.Sqrt(variance);
        var sigmaDaily = sigmaMonthly / Math.Sqrt(30.4375);

        var lead = Math.Max(1, p.LeadTimeDays);
        var z = ZFactor(p.TargetServiceLevel);

        var leadTimeDemand = add * lead;
        var safety = z * sigmaDaily * Math.Sqrt(lead);
        if (p.ManualBuffer > safety) safety = p.ManualBuffer;
        var orderLevel = leadTimeDemand + safety;

        var review = Math.Max(1, p.ReviewPeriodDays);
        var targetStock = orderLevel + add * review;

        var onHand = stockRow.QtyOnHand;
        var effectiveStock = onHand;

        var rawRoq = 0.0;
        if (effectiveStock <= orderLevel) rawRoq = targetStock - effectiveStock;
        var roq = RoundToMultiple(rawRoq, p.MinOrderQty, p.OrderMultiple);
        var value = Math.Round(roq * stockRow.AvgUnitCost, 2);
        _ = cls;

        return new Metrics(itemId, warehouseId, Math.Round(roq, 3), value, p.Supplier);
    }

    private async Task<List<double>> DemandSeriesAsync(DbConnection connection, long itemId, long warehouseId, int months, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var since = now.AddMonths(-months).ToUnixTimeSeconds();
        var byMonth = new Dictionary<string, double>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT FROM_UNIXTIME(`movement_date`, '%Y-%m') AS ym, SUM(`qty`) AS q FROM `epc_erp_inv_movements` WHERE `movement_type` IN ('sale_out','transfer_out') AND `active`=1 AND `item_id`=? AND `movement_date`>=?" +
                (warehouseId > 0 ? " AND `warehouse_id`=?" : "") + " GROUP BY ym";
            command.CommandText = ErpDb.Positional(command.CommandText);
            ErpDb.AddParameters(command, warehouseId > 0
                ? new object[] { itemId, since, warehouseId }
                : new object[] { itemId, since });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                byMonth[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? ""] = Math.Abs(Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture));
            }
        }
        var series = new List<double>(months);
        for (var i = months - 1; i >= 0; i--)
        {
            var key = now.AddMonths(-i).ToString("yyyy-MM", CultureInfo.InvariantCulture);
            series.Add(byMonth.TryGetValue(key, out var q) ? q : 0.0);
        }
        return series;
    }

    private static (string Class, double Adi, double Cv2) ClassifyDemand(IReadOnlyList<double> series)
    {
        var n = series.Count;
        var nonzero = series.Where(v => v > 0).ToList();
        var k = nonzero.Count;
        if (k == 0) return ("no demand", 0.0, 0.0);
        var adi = (double)n / k;
        var mean = nonzero.Sum() / k;
        var varSum = nonzero.Sum(v => (v - mean) * (v - mean));
        var variance = k > 1 ? varSum / k : 0.0;
        var cv2 = mean > 0 ? variance / (mean * mean) : 0.0;
        var cls = adi < 1.32 && cv2 < 0.49 ? "smooth"
            : adi < 1.32 && cv2 >= 0.49 ? "erratic"
            : adi >= 1.32 && cv2 < 0.49 ? "intermittent"
            : "lumpy";
        return (cls, Math.Round(adi, 2), Math.Round(cv2, 2));
    }

    private async Task<Params> ParamsGetAsync(DbConnection connection, long itemId, long warehouseId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT * FROM `epc_erp_planning_params` WHERE `item_id`=? AND `warehouse_id`=?");
        ErpDb.AddParameters(command, itemId, warehouseId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new Params(
                Convert.ToInt64(reader["lead_time_days"]),
                Convert.ToDouble(reader["target_service_level"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["review_period_days"]),
                Convert.ToDouble(reader["min_order_qty"], CultureInfo.InvariantCulture),
                Convert.ToDouble(reader["order_multiple"], CultureInfo.InvariantCulture),
                Convert.ToDouble(reader["manual_buffer"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["supplier"], CultureInfo.InvariantCulture) ?? "");
        }
        return new Params(30, 90.0, 30, 0.0, 0.0, 0.0, "");
    }

    private static double RoundToMultiple(double qty, double minQty, double multiple)
    {
        if (qty <= 0) return 0.0;
        if (minQty > 0 && qty < minQty) qty = minQty;
        return multiple > 0 ? Math.Ceiling(qty / multiple) * multiple : Math.Ceiling(qty);
    }

    private static double ZFactor(double servicePct)
    {
        var sl = servicePct / 100.0;
        double[] levels = { 0.50, 0.75, 0.80, 0.85, 0.90, 0.925, 0.95, 0.975, 0.98, 0.99, 0.995, 0.999 };
        double[] zs = { 0.00, 0.67, 0.84, 1.04, 1.28, 1.44, 1.65, 1.96, 2.05, 2.33, 2.58, 3.09 };
        var best = 1.28;
        var bestDiff = 1e9;
        for (var i = 0; i < levels.Length; i++)
        {
            var d = Math.Abs(levels[i] - sl);
            if (d < bestDiff) { bestDiff = d; best = zs[i]; }
        }
        return best;
    }

    private static int NextRand(Random rng, int min, int max) => rng.Next(min, max + 1);

    private static async Task<List<StockRow>> StockReportAsync(DbConnection connection, long warehouseId, CancellationToken cancellationToken)
    {
        var rows = new List<StockRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT s.*, i.sku, i.name, i.item_type, i.unit, w.name AS warehouse_name FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.id = s.item_id INNER JOIN `epc_erp_inv_warehouses` w ON w.id = s.warehouse_id WHERE i.active = 1" +
            (warehouseId > 0 ? " AND s.warehouse_id = ?" : "") + " ORDER BY w.name, i.sku, s.batch_no";
        command.CommandText = ErpDb.Positional(command.CommandText);
        if (warehouseId > 0) ErpDb.AddParameters(command, warehouseId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new StockRow(
                Convert.ToInt64(reader["item_id"]),
                Convert.ToInt64(reader["warehouse_id"]),
                Convert.ToString(reader["sku"], CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader["unit"], CultureInfo.InvariantCulture) ?? "pcs",
                Convert.ToString(reader["warehouse_name"], CultureInfo.InvariantCulture) ?? "",
                Convert.ToDouble(reader["qty_on_hand"], CultureInfo.InvariantCulture),
                Convert.ToDouble(reader["avg_unit_cost"], CultureInfo.InvariantCulture)));
        }
        return rows;
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_planning_params` (`item_id` int(11) NOT NULL, `warehouse_id` int(11) NOT NULL, `lead_time_days` int(11) NOT NULL DEFAULT 30, `target_service_level` decimal(5,2) NOT NULL DEFAULT 90.00, `review_period_days` int(11) NOT NULL DEFAULT 30, `min_order_qty` decimal(14,3) NOT NULL DEFAULT 0.000, `order_multiple` decimal(14,3) NOT NULL DEFAULT 0.000, `manual_buffer` decimal(14,3) NOT NULL DEFAULT 0.000, `supplier` varchar(160) NOT NULL DEFAULT '', `stocked` tinyint(1) NOT NULL DEFAULT 1, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`item_id`,`warehouse_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per item/warehouse planning parameters'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_order_recommendations` (`id` int(11) NOT NULL AUTO_INCREMENT, `item_id` int(11) NOT NULL, `warehouse_id` int(11) NOT NULL, `roq` decimal(14,3) NOT NULL DEFAULT 0.000, `order_value` decimal(16,2) NOT NULL DEFAULT 0.00, `status` varchar(12) NOT NULL DEFAULT 'pending', `supplier` varchar(160) NOT NULL DEFAULT '', `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `x_item_wh` (`item_id`,`warehouse_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Replenishment recommendation status'",
            cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW COLUMNS FROM `epc_erp_order_recommendations` LIKE 'ordered_po_id'";
            var col = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (col is null)
            {
                await ErpDb.ExecuteAsync(connection, null,
                    "ALTER TABLE `epc_erp_order_recommendations` ADD `ordered_po_id` int(11) NOT NULL DEFAULT 0",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
        }
    }
}
