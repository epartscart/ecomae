using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>mfg_wo_issue</c> and <c>mfg_wo_complete</c> twins
/// (content/shop/finance/epc_erp_manufacturing.php).
/// Inventory movements go through the shared inv_record_movement core and are
/// swallowed on failure, exactly like PHP's <c>function_exists</c> + try/catch
/// tracking-only fallback.
/// </summary>
public interface IErpMfgWriteService
{
    Task<ErpMfgWoIssueResult> IssueMaterialsAsync(long workOrderId, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpMfgWoCompleteResult> CompleteAsync(
        long workOrderId,
        decimal qtyProduced,
        int adminUserId,
        decimal labourCost = -1m,
        decimal overheadCost = -1m,
        CancellationToken cancellationToken = default);
}

public sealed record ErpMfgIssuedLine(long ComponentItemId, decimal Qty, decimal UnitCost);

public sealed record ErpMfgWoIssueResult(
    bool Ok,
    string Message,
    long WorkOrderId,
    decimal MaterialCost,
    IReadOnlyList<ErpMfgIssuedLine> Issued);

public sealed record ErpMfgWoCompleteResult(
    bool Ok,
    string Message,
    long WorkOrderId,
    decimal QtyProduced,
    decimal MaterialCost,
    decimal LabourCost,
    decimal OverheadCost,
    decimal TotalCost,
    decimal UnitCost);

public sealed class ErpMfgWriteService : IErpMfgWriteService
{
    private static readonly string[] SchemaStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS `epc_mfg_bom` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `product_item_id` int(11) NOT NULL,
            `name` varchar(160) NOT NULL DEFAULT '',
            `output_qty` decimal(14,4) NOT NULL DEFAULT 1.0000,
            `labour_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `overhead_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_product` (`product_item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bill of materials header'
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_mfg_bom_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `bom_id` int(11) NOT NULL,
            `component_item_id` int(11) NOT NULL,
            `qty_per` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `scrap_percent` decimal(7,3) NOT NULL DEFAULT 0.000,
            PRIMARY KEY (`id`),
            KEY `x_bom` (`bom_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOM components'
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_mfg_work_orders` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `wo_no` varchar(40) NOT NULL DEFAULT '',
            `bom_id` int(11) NOT NULL,
            `product_item_id` int(11) NOT NULL,
            `qty_planned` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `qty_produced` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(16) NOT NULL DEFAULT 'planned',
            `material_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `labour_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `overhead_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Work orders'
        """,
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpMfgWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var ddl in SchemaStatements)
        {
            await ErpDb.TryExecuteAsync(connection, ddl, cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    public async Task<ErpMfgWoIssueResult> IssueMaterialsAsync(long workOrderId, int adminUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var wo = await GetWorkOrderAsync(connection, workOrderId, cancellationToken).ConfigureAwait(false);
        if (wo is null)
        {
            return new ErpMfgWoIssueResult(false, "Work order not found", 0, 0m, []);
        }

        var outputQty = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `output_qty` FROM `epc_mfg_bom` WHERE `id` = ?"),
            cancellationToken,
            wo.BomId).ConfigureAwait(false);
        var factor = wo.QtyPlanned / Math.Max(0.0001m, outputQty);

        var requirements = new List<(long ComponentItemId, decimal QtyRequired)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `component_item_id`, `qty_per`, `scrap_percent` FROM `epc_mfg_bom_lines` WHERE `bom_id` = @p0";
            ErpDb.AddParameters(command, wo.BomId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var baseQty = reader.GetDecimal(1) * factor;
                var withScrap = baseQty * (1m + reader.GetDecimal(2) / 100m);
                requirements.Add((reader.GetInt64(0), Math.Round(withScrap, 4, MidpointRounding.AwayFromZero)));
            }
        }

        var materialCost = 0m;
        var issued = new List<ErpMfgIssuedLine>();
        foreach (var (itemId, qty) in requirements)
        {
            var unit = await ItemCostAsync(connection, itemId, wo.WarehouseId, cancellationToken).ConfigureAwait(false);
            materialCost += qty * unit;
            try
            {
                await ErpInventoryMovementWriteService.RecordMovementCoreAsync(
                    connection,
                    new ErpInventoryMovementWriteRequest(
                        adminUserId,
                        "mfg_issue",
                        wo.WarehouseId,
                        itemId,
                        -qty,
                        unit,
                        Reference: wo.WoNo,
                        MovementDate: DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    "mfg_issue",
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // PHP swallows movement failures — tracking-only fallback.
            }

            issued.Add(new ErpMfgIssuedLine(itemId, qty, Math.Round(unit, 4, MidpointRounding.AwayFromZero)));
        }

        materialCost = Math.Round(materialCost, 2, MidpointRounding.AwayFromZero);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_mfg_work_orders` SET `material_cost` = ?, `status` = 'in_progress', `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            materialCost,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            workOrderId).ConfigureAwait(false);

        var message = "Materials issued — cost " + materialCost.ToString("N2", CultureInfo.InvariantCulture) + " AED";
        return new ErpMfgWoIssueResult(true, message, workOrderId, materialCost, issued);
    }

    public async Task<ErpMfgWoCompleteResult> CompleteAsync(
        long workOrderId,
        decimal qtyProduced,
        int adminUserId,
        decimal labourCost = -1m,
        decimal overheadCost = -1m,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var wo = await GetWorkOrderAsync(connection, workOrderId, cancellationToken).ConfigureAwait(false);
        if (wo is null)
        {
            return new ErpMfgWoCompleteResult(false, "Work order not found", 0, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        decimal bomOutput = 1m, bomLabour = 0m, bomOverhead = 0m;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `output_qty`, `labour_cost`, `overhead_cost` FROM `epc_mfg_bom` WHERE `id` = @p0";
            ErpDb.AddParameters(command, wo.BomId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                bomOutput = reader.GetDecimal(0);
                bomLabour = reader.GetDecimal(1);
                bomOverhead = reader.GetDecimal(2);
            }
        }

        var scale = qtyProduced / Math.Max(0.0001m, bomOutput);
        if (labourCost < 0m)
        {
            labourCost = Math.Round(bomLabour * scale, 2, MidpointRounding.AwayFromZero);
        }
        if (overheadCost < 0m)
        {
            overheadCost = Math.Round(bomOverhead * scale, 2, MidpointRounding.AwayFromZero);
        }

        var materialCost = wo.MaterialCost;
        var totalCost = Math.Round(materialCost + labourCost + overheadCost, 2, MidpointRounding.AwayFromZero);
        var unitCost = qtyProduced > 0m ? Math.Round(totalCost / qtyProduced, 4, MidpointRounding.AwayFromZero) : 0m;

        try
        {
            await ErpInventoryMovementWriteService.RecordMovementCoreAsync(
                connection,
                new ErpInventoryMovementWriteRequest(
                    adminUserId,
                    "mfg_receipt",
                    wo.WarehouseId,
                    wo.ProductItemId,
                    qtyProduced,
                    unitCost,
                    Reference: wo.WoNo,
                    MovementDate: DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                "mfg_receipt",
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // PHP swallows movement failures — tracking-only fallback.
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_mfg_work_orders` SET `qty_produced` = ?, `labour_cost` = ?, `overhead_cost` = ?, `status` = 'completed', `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            qtyProduced,
            labourCost,
            overheadCost,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            workOrderId).ConfigureAwait(false);

        var message = "Work order completed — unit cost " + unitCost.ToString("N4", CultureInfo.InvariantCulture) + " AED";
        return new ErpMfgWoCompleteResult(true, message, workOrderId, qtyProduced, Math.Round(materialCost, 2), labourCost, overheadCost, totalCost, unitCost);
    }

    private static async Task<ErpMfgWorkOrderRow?> GetWorkOrderAsync(DbConnection connection, long woId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `wo_no`, `bom_id`, `product_item_id`, `qty_planned`, `warehouse_id`, `material_cost` FROM `epc_mfg_work_orders` WHERE `id` = @p0";
        ErpDb.AddParameters(command, woId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ErpMfgWorkOrderRow(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetDecimal(4),
            reader.GetInt64(5),
            reader.GetDecimal(6));
    }

    private static async Task<decimal> ItemCostAsync(DbConnection connection, long itemId, long warehouseId, CancellationToken cancellationToken)
    {
        try
        {
            var sql = warehouseId > 0
                ? "SELECT `avg_unit_cost` FROM `epc_erp_inv_stock` WHERE `item_id` = ? AND `warehouse_id` = ? LIMIT 1"
                : "SELECT `avg_unit_cost` FROM `epc_erp_inv_stock` WHERE `item_id` = ? ORDER BY `avg_unit_cost` DESC LIMIT 1";
            var value = warehouseId > 0
                ? await ErpDb.ScalarAsync(connection, null, ErpDb.Positional(sql), cancellationToken, itemId, warehouseId).ConfigureAwait(false)
                : await ErpDb.ScalarAsync(connection, null, ErpDb.Positional(sql), cancellationToken, itemId).ConfigureAwait(false);
            return value is null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return 0m;
        }
    }

    private sealed record ErpMfgWorkOrderRow(
        long Id,
        string WoNo,
        long BomId,
        long ProductItemId,
        decimal QtyPlanned,
        long WarehouseId,
        decimal MaterialCost);
}
