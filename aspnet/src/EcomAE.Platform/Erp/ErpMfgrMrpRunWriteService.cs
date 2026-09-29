using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_mfgr_mrp_run</c> twin. PHP remains the schema owner.</summary>
public interface IErpMfgrMrpRunWriteService
{
    Task<ErpSimpleWriteResult> RunAsync(
        long companyId,
        IReadOnlyDictionary<long, decimal> demand,
        IReadOnlyDictionary<long, decimal> onHand,
        long dueDate,
        CancellationToken cancellationToken = default);
}

public sealed class ErpMfgrMrpRunWriteService : IErpMfgrMrpRunWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMfgrMrpRunWriteService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpSimpleWriteResult> RunAsync(
        long companyId,
        IReadOnlyDictionary<long, decimal> demand,
        IReadOnlyDictionary<long, decimal> onHand,
        long dueDate,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) return ErpSimpleWriteResult.Fail("invalid", "A company id is required.");
        if (demand.Count == 0) return ErpSimpleWriteResult.Fail("invalid", "At least one demand item is required.");
        if (!_connections.IsConfigured) return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var bomCache = new Dictionary<long, Bom?>();
        var orders = new List<Planned>();
        var available = onHand.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in demand)
            await ExplodeAsync(pair.Key, pair.Value, available, bomCache, orders, 0, cancellationToken).ConfigureAwait(false);

        var aggregate = new Dictionary<(long ItemId, string Type), Planned>();
        foreach (var order in orders)
        {
            var key = (order.ItemId, order.OrderType);
            if (aggregate.TryGetValue(key, out var existing))
                aggregate[key] = existing with { Qty = decimal.Round(existing.Qty + order.Qty, 4), Level = Math.Max(existing.Level, order.Level) };
            else
                aggregate[key] = order;
        }

        var ordered = aggregate.Values.OrderByDescending(order => order.Level).ThenBy(order => order.ItemId).ToArray();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `epc_mfg_planned` WHERE `company_id`=? AND `source`='mrp'"),
            cancellationToken,
            companyId).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var order in ordered)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `epc_mfg_planned` (`company_id`,`item_id`,`order_type`,`qty`,`level`,`due_date`,`source`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                companyId,
                order.ItemId,
                order.OrderType,
                order.Qty,
                order.Level,
                dueDate,
                "mrp",
                "planned",
                now).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("MRP planned orders regenerated", companyId) with { Writes = deleted + ordered.Length };
    }

    private async Task ExplodeAsync(
        long itemId,
        decimal required,
        Dictionary<long, decimal> onHand,
        Dictionary<long, Bom?> bomCache,
        List<Planned> orders,
        int level,
        CancellationToken cancellationToken)
    {
        if (itemId <= 0 || required <= 0 || level > 20) return;
        onHand.TryGetValue(itemId, out var available);
        var consumed = Math.Min(available, required);
        onHand[itemId] = available - consumed;
        var net = decimal.Round(required - consumed, 4);
        if (net <= 0) return;

        if (!bomCache.TryGetValue(itemId, out var bom))
        {
            bom = await LoadBomAsync(itemId, cancellationToken).ConfigureAwait(false);
            bomCache[itemId] = bom;
        }

        if (bom is null || bom.Lines.Count == 0)
        {
            orders.Add(new(itemId, "purchase", net, level));
            return;
        }

        orders.Add(new(itemId, "production", net, level));
        var batch = net / (bom.OutputQty > 0 ? bom.OutputQty : 1);
        foreach (var line in bom.Lines)
        {
            var child = decimal.Round(batch * line.QtyPer * (1 + line.ScrapPercent / 100), 4);
            await ExplodeAsync(line.ItemId, child, onHand, bomCache, orders, level + 1, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Bom?> LoadBomAsync(long itemId, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        long bomId;
        decimal outputQty;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`,`output_qty` FROM `epc_mfg_bom` WHERE `product_item_id`=? AND `active`=1 ORDER BY `id` DESC LIMIT 1";
            Add(command, itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
            bomId = reader.GetInt64(0);
            outputQty = reader.IsDBNull(1) ? 1 : reader.GetDecimal(1);
        }

        var lines = new List<BomLine>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `component_item_id`,`qty_per`,`scrap_percent` FROM `epc_mfg_bom_lines` WHERE `bom_id`=?";
            Add(command, bomId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                lines.Add(new(reader.GetInt64(0), reader.GetDecimal(1), reader.IsDBNull(2) ? 0 : reader.GetDecimal(2)));
        }

        return new(outputQty, lines);
    }

    private static void Add(DbCommand command, object value)
    {
        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record Bom(decimal OutputQty, IReadOnlyList<BomLine> Lines);
    private sealed record BomLine(long ItemId, decimal QtyPer, decimal ScrapPercent);
    private sealed record Planned(long ItemId, string OrderType, decimal Qty, int Level);
}
