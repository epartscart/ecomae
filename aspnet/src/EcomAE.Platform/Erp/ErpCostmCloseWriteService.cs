using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_costm_close_run</c> twin (epc_erp_cost_models.php): runs the item's
/// costing model over its transactions and persists an <c>epc_costm_close</c> record.
/// </summary>
public interface IErpCostmCloseWriteService
{
    Task<ErpCostmCloseResult> CloseRunAsync(long itemId, string? label, CancellationToken cancellationToken = default);
}

public sealed record ErpCostmCloseResult(
    bool Ok,
    string Message,
    long Id,
    string Model,
    decimal Cogs,
    decimal ClosingQty,
    decimal ClosingValue,
    decimal Variance,
    int Writes);

public sealed class ErpCostmCloseWriteService : IErpCostmCloseWriteService
{
    private static readonly string[] Models = { "standard", "fifo", "lifo", "moving_avg" };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpCostmCloseWriteService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ErpCostmCloseResult> CloseRunAsync(long itemId, string? label, CancellationToken cancellationToken = default)
    {
        if (itemId <= 0) return new(false, "item_id is required", 0, string.Empty, 0m, 0m, 0m, 0m, 0);
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", 0, string.Empty, 0m, 0m, 0m, 0m, 0);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var companyId = await ErpFinAdvancedCompany.ResolveAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        var model = "moving_avg";
        var stdCost = 0m;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `model`,`std_cost` FROM `epc_costm_item` WHERE `company_id`=? AND `item_id`=?");
            ErpDb.AddParameters(command, companyId, itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                model = Convert.ToString(reader.GetValue(0)) ?? model;
                stdCost = Convert.ToDecimal(reader.GetValue(1));
            }
        }

        var txns = new List<(string Type, decimal Qty, decimal UnitCost)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `txn_type`,`qty`,`unit_cost` FROM `epc_costm_txn` WHERE `company_id`=? AND `item_id`=? ORDER BY `txn_date` ASC, `id` ASC");
            ErpDb.AddParameters(command, companyId, itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                txns.Add((Convert.ToString(reader.GetValue(0)) ?? "receipt", Convert.ToDecimal(reader.GetValue(1)), Convert.ToDecimal(reader.GetValue(2))));
            }
        }

        var res = Compute(model, txns, stdCost);

        var now = _clock.GetUtcNow();
        var effectiveLabel = string.IsNullOrWhiteSpace(label) ? now.ToString("yyyy-MM", CultureInfo.InvariantCulture) : label.Trim();
        var detailJson = JsonSerializer.Serialize(new
        {
            res.Model,
            cogs = res.Cogs,
            closing_qty = res.ClosingQty,
            closing_value = res.ClosingValue,
            variance = res.Variance,
            issues = res.Issues,
            layers = res.Layers,
        });
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_costm_close` (`company_id`,`item_id`,`model`,`label`,`cogs`,`closing_qty`,`closing_value`,`variance`,`detail_json`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            itemId,
            res.Model,
            effectiveLabel,
            res.Cogs,
            res.ClosingQty,
            res.ClosingValue,
            res.Variance,
            detailJson,
            now.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        var message = "Closing: COGS " + res.Cogs.ToString("0.00", CultureInfo.InvariantCulture)
            + " · closing value " + res.ClosingValue.ToString("0.00", CultureInfo.InvariantCulture);
        return new(true, message, id, res.Model, res.Cogs, res.ClosingQty, res.ClosingValue, res.Variance, writes);
    }

    /// <summary>PHP <c>epc_costm_compute</c> — verbatim port (standard / fifo / lifo / moving_avg).</summary>
    internal static CostmComputeResult Compute(string model, IReadOnlyList<(string Type, decimal Qty, decimal UnitCost)> txns, decimal stdCost)
    {
        if (!Models.Contains(model)) model = "moving_avg";

        var cogs = 0m;
        var variance = 0m;
        var issues = new List<object>();
        var layers = new List<(decimal Qty, decimal Cost)>();
        var avgQty = 0m;
        var avgVal = 0m;
        var lastCost = stdCost;

        foreach (var t in txns)
        {
            var type = t.Type;
            var qty = t.Qty;
            var unit = t.UnitCost;
            if (qty <= 0) continue;
            if (type == "receipt")
            {
                lastCost = unit;
                if (model == "standard")
                    variance += Math.Round(qty * (unit - stdCost), 4, MidpointRounding.AwayFromZero);
                else if (model == "moving_avg")
                {
                    avgQty += qty;
                    avgVal += qty * unit;
                }
                else
                    layers.Add((qty, unit));
                continue;
            }

            // issue
            var issueCost = 0m;
            if (model == "standard")
            {
                issueCost = Math.Round(qty * stdCost, 4, MidpointRounding.AwayFromZero);
            }
            else if (model == "moving_avg")
            {
                var avg = avgQty > 0 ? avgVal / avgQty : lastCost;
                issueCost = Math.Round(qty * avg, 4, MidpointRounding.AwayFromZero);
                avgQty -= qty;
                avgVal -= issueCost;
                if (avgQty <= 0)
                {
                    avgQty = 0m;
                    avgVal = 0m;
                }
            }
            else // fifo / lifo
            {
                var need = qty;
                while (need > 0 && layers.Count > 0)
                {
                    var idx = model == "fifo" ? 0 : layers.Count - 1;
                    var layer = layers[idx];
                    var take = Math.Min(need, layer.Qty);
                    issueCost += take * layer.Cost;
                    var remaining = layer.Qty - take;
                    need -= take;
                    if (remaining <= 0.00001m)
                        layers.RemoveAt(idx);
                    else
                        layers[idx] = (remaining, layer.Cost);
                }
                if (need > 0)
                {
                    // shortfall valued at last known cost (PHP quirk)
                    issueCost += need * lastCost;
                }
                issueCost = Math.Round(issueCost, 4, MidpointRounding.AwayFromZero);
            }
            cogs += issueCost;
            issues.Add(new { qty, cost = Math.Round(issueCost, 2, MidpointRounding.AwayFromZero), unit = qty > 0 ? Math.Round(issueCost / qty, 4, MidpointRounding.AwayFromZero) : 0m });
        }

        decimal closingQty;
        decimal closingValue;
        if (model == "standard")
        {
            closingQty = 0m;
            foreach (var t in txns)
            {
                closingQty += t.Type == "receipt" ? t.Qty : -t.Qty;
            }
            closingValue = Math.Round(closingQty * stdCost, 2, MidpointRounding.AwayFromZero);
        }
        else if (model == "moving_avg")
        {
            closingQty = avgQty;
            closingValue = Math.Round(avgVal, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            closingQty = 0m;
            var cv = 0m;
            foreach (var l in layers)
            {
                closingQty += l.Qty;
                cv += l.Qty * l.Cost;
            }
            closingValue = Math.Round(cv, 2, MidpointRounding.AwayFromZero);
        }

        return new CostmComputeResult(
            model,
            Math.Round(cogs, 2, MidpointRounding.AwayFromZero),
            Math.Round(closingQty, 4, MidpointRounding.AwayFromZero),
            closingValue,
            Math.Round(variance, 2, MidpointRounding.AwayFromZero),
            issues,
            layers.Select(l => new { qty = l.Qty, cost = l.Cost }).Cast<object>().ToList());
    }

    internal sealed record CostmComputeResult(
        string Model,
        decimal Cogs,
        decimal ClosingQty,
        decimal ClosingValue,
        decimal Variance,
        List<object> Issues,
        List<object> Layers);

    /// <summary>PHP <c>epc_costm_ensure_schema</c> — verbatim.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_costm_item` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `model` varchar(16) NOT NULL DEFAULT 'moving_avg',
            `std_cost` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_item` (`company_id`,`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-item costing model assignment'", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_costm_txn` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `txn_type` varchar(8) NOT NULL DEFAULT 'receipt',
            `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `unit_cost` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `txn_date` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Costing model transactions'", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_costm_close` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `model` varchar(16) NOT NULL DEFAULT 'moving_avg',
            `label` varchar(40) NOT NULL DEFAULT '',
            `cogs` decimal(18,2) NOT NULL DEFAULT 0.00,
            `closing_qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `closing_value` decimal(18,2) NOT NULL DEFAULT 0.00,
            `variance` decimal(18,2) NOT NULL DEFAULT 0.00,
            `detail_json` mediumtext,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inventory closing/recalculation runs'", ct).ConfigureAwait(false);
    }
}
