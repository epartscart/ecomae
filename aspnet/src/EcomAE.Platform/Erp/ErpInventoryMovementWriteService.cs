using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>inv_record_movement</c> / <c>inv_transfer</c> twins.
/// Create warehouse/item, CSV import, and closing stay Classic. Schema-ensure stays PHP.
/// </summary>
public interface IErpInventoryMovementWriteService
{
    Task<ErpSimpleWriteResult> RecordMovementAsync(
        ErpInventoryMovementWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> TransferAsync(
        ErpInventoryTransferWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpInventoryMovementWriteRequest(
    int AdminUserId = 0,
    string? MovementType = null,
    long WarehouseId = 0,
    long ItemId = 0,
    decimal Qty = 0,
    decimal UnitCost = 0,
    string? BatchNo = null,
    string? VariantLabel = null,
    string? ExpiryDate = null,
    string? SerialNo = null,
    string? Reference = null,
    string? Note = null,
    string? MovementDate = null,
    long TransferWarehouseId = 0,
    long PurchaseId = 0,
    long OrderId = 0,
    long OpeningBatchId = 0);

public sealed record ErpInventoryTransferWriteRequest(
    int AdminUserId = 0,
    long FromWarehouseId = 0,
    long ToWarehouseId = 0,
    long ItemId = 0,
    decimal Qty = 0,
    string? BatchNo = null,
    string? VariantLabel = null,
    string? Reference = null,
    string? Note = null);

public sealed class ErpInventoryMovementWriteService : IErpInventoryMovementWriteService
{
    private static readonly HashSet<string> InTypes =
        ["opening", "purchase_in", "transfer_in", "return_in", "adjustment"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpInventoryMovementWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RecordMovementAsync(
        ErpInventoryMovementWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = string.IsNullOrWhiteSpace(request.MovementType) ? "adjustment" : request.MovementType.Trim();
        if (request.WarehouseId <= 0 || request.ItemId <= 0 || request.Qty == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse, item and quantity required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var id = await RecordMovementCoreAsync(connection, request, type, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Inventory movement recorded", id);
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> TransferAsync(
        ErpInventoryTransferWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FromWarehouseId <= 0 || request.ToWarehouseId <= 0 || request.FromWarehouseId == request.ToWarehouseId)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Source and destination warehouses required (must differ)");
        }

        if (request.ItemId <= 0 || request.Qty <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item and positive quantity required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var batch = (request.BatchNo ?? string.Empty).Trim();
        var variant = (request.VariantLabel ?? string.Empty).Trim();
        var reference = (request.Reference ?? string.Empty).Trim();
        if (reference.Length == 0)
        {
            reference = "TRF-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var row = await GetStockRowAsync(connection, request.FromWarehouseId, request.ItemId, batch, variant, cancellationToken)
                .ConfigureAwait(false);
            if (row is null || row.QtyOnHand < request.Qty)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Insufficient stock at source warehouse");
            }

            var unitCost = row.AvgUnitCost;
            var note = (request.Note ?? string.Empty).Trim();
            var outId = await RecordMovementCoreAsync(
                connection,
                new ErpInventoryMovementWriteRequest(
                    request.AdminUserId,
                    "transfer_out",
                    request.FromWarehouseId,
                    request.ItemId,
                    request.Qty,
                    unitCost,
                    batch,
                    variant,
                    null,
                    null,
                    reference,
                    note,
                    null,
                    request.ToWarehouseId),
                "transfer_out",
                cancellationToken).ConfigureAwait(false);
            await RecordMovementCoreAsync(
                connection,
                new ErpInventoryMovementWriteRequest(
                    request.AdminUserId,
                    "transfer_in",
                    request.ToWarehouseId,
                    request.ItemId,
                    request.Qty,
                    unitCost,
                    batch,
                    variant,
                    null,
                    null,
                    reference,
                    note,
                    null,
                    request.FromWarehouseId),
                "transfer_in",
                cancellationToken).ConfigureAwait(false);
            var costText = unitCost.ToString("0.0000", CultureInfo.InvariantCulture);
            return ErpSimpleWriteResult.Ok("Warehouse transfer completed at avg cost " + costText, outId);
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
    }

    private static async Task<long> RecordMovementCoreAsync(
        DbConnection connection,
        ErpInventoryMovementWriteRequest request,
        string type,
        CancellationToken cancellationToken)
    {
        var qty = request.Qty;
        var unitCost = request.UnitCost;
        var qtyAbs = Math.Abs(qty);
        var batch = (request.BatchNo ?? string.Empty).Trim();
        var variant = (request.VariantLabel ?? string.Empty).Trim();
        var expiry = string.IsNullOrWhiteSpace(request.ExpiryDate) ? null : request.ExpiryDate.Trim();
        var incoming = (type == "adjustment" && qty > 0)
                       || (InTypes.Contains(type) && qty > 0 && type != "adjustment");

        if (incoming)
        {
            await UpsertStockAsync(connection, request.WarehouseId, request.ItemId, qtyAbs, unitCost, batch, variant, expiry, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var row = await GetStockRowAsync(connection, request.WarehouseId, request.ItemId, batch, variant, cancellationToken)
                .ConfigureAwait(false);
            if (row is null || row.QtyOnHand < qtyAbs)
            {
                throw new ErpWriteException("Insufficient quantity on hand");
            }

            unitCost = row.AvgUnitCost;
            await UpsertStockAsync(connection, request.WarehouseId, request.ItemId, -qtyAbs, unitCost, batch, variant, expiry, cancellationToken)
                .ConfigureAwait(false);
        }

        var total = Math.Round(qtyAbs * unitCost, 2, MidpointRounding.AwayFromZero);
        var movementDate = ParseMovementDate(request.MovementDate);
        var serial = (request.SerialNo ?? string.Empty).Trim();
        var hasSerial = await HasColumnAsync(connection, "epc_erp_inv_movements", "serial_no", cancellationToken).ConfigureAwait(false);
        if (hasSerial)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_erp_inv_movements`
                    (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`transfer_warehouse_id`,`purchase_id`,`order_id`,
                    `batch_no`,`expiry_date`,`serial_no`,`reference`,`note`,`movement_date`,`admin_id`,`opening_batch_id`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                type,
                request.WarehouseId,
                request.ItemId,
                qtyAbs,
                unitCost,
                total,
                request.TransferWarehouseId,
                request.PurchaseId,
                request.OrderId,
                batch.Length == 0 ? null : batch,
                expiry,
                serial.Length == 0 ? null : serial,
                (request.Reference ?? string.Empty).Trim(),
                (request.Note ?? string.Empty).Trim(),
                movementDate,
                request.AdminUserId,
                request.OpeningBatchId);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_erp_inv_movements`
                    (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`transfer_warehouse_id`,`purchase_id`,`order_id`,
                    `batch_no`,`expiry_date`,`reference`,`note`,`movement_date`,`admin_id`,`opening_batch_id`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                type,
                request.WarehouseId,
                request.ItemId,
                qtyAbs,
                unitCost,
                total,
                request.TransferWarehouseId,
                request.PurchaseId,
                request.OrderId,
                batch.Length == 0 ? null : batch,
                expiry,
                (request.Reference ?? string.Empty).Trim(),
                (request.Note ?? string.Empty).Trim(),
                movementDate,
                request.AdminUserId,
                request.OpeningBatchId);
        }

        var movementId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (serial.Length > 0)
        {
            var serialIn = InTypes.Contains(type) && type != "adjustment" || (type == "adjustment" && qty > 0);
            await RegisterSerialAsync(
                connection,
                request.ItemId,
                serial,
                request.WarehouseId,
                batch,
                unitCost,
                movementId,
                serialIn,
                cancellationToken).ConfigureAwait(false);
        }

        return movementId;
    }

    private static async Task UpsertStockAsync(
        DbConnection connection,
        long warehouseId,
        long itemId,
        decimal qtyDelta,
        decimal unitCost,
        string batchNo,
        string variant,
        string? expiry,
        CancellationToken cancellationToken)
    {
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sql = "SELECT `id`, `qty_on_hand`, `avg_unit_cost` FROM `epc_erp_inv_stock` WHERE `warehouse_id` = ? AND `item_id` = ?";
            var args = new List<object?> { warehouseId, itemId };
            if (batchNo.Length > 0)
            {
                sql += " AND `batch_no` = ?";
                args.Add(batchNo);
            }
            else
            {
                sql += " AND (`batch_no` IS NULL OR `batch_no` = '')";
            }

            if (variant.Length > 0)
            {
                sql += " AND `variant_label` = ?";
                args.Add(variant);
            }
            else
            {
                sql += " AND (`variant_label` IS NULL OR `variant_label` = '')";
            }

            sql += " LIMIT 1 FOR UPDATE";
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(cmd, args.ToArray());
            long stockId = 0;
            decimal oldQty = 0;
            decimal oldAvg = 0;
            var found = false;
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    found = true;
                    stockId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    oldQty = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                    oldAvg = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
                }
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!found)
            {
                var avg = qtyDelta > 0 ? unitCost : 0m;
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("""
                        INSERT INTO `epc_erp_inv_stock`
                        (`warehouse_id`,`item_id`,`qty_on_hand`,`avg_unit_cost`,`batch_no`,`variant_label`,`expiry_date`,`time_updated`)
                        VALUES (?,?,?,?,?,?,?,?)
                        """),
                    cancellationToken,
                    warehouseId,
                    itemId,
                    Math.Max(0, qtyDelta),
                    avg,
                    batchNo.Length == 0 ? null : batchNo,
                    variant.Length == 0 ? null : variant,
                    string.IsNullOrWhiteSpace(expiry) ? null : expiry,
                    now);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var newQty = oldQty + qtyDelta;
            if (newQty < 0)
            {
                throw new ErpWriteException("Insufficient stock (would go negative)");
            }

            var newAvg = oldAvg;
            if (qtyDelta > 0)
            {
                newAvg = ApplyWeightedAverage(oldQty, oldAvg, qtyDelta, unitCost);
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `epc_erp_inv_stock` SET `qty_on_hand` = ?, `avg_unit_cost` = ?, `expiry_date` = COALESCE(?, `expiry_date`), `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                newQty,
                newAvg,
                string.IsNullOrWhiteSpace(expiry) ? null : expiry,
                now,
                stockId);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static decimal ApplyWeightedAverage(decimal oldQty, decimal oldCost, decimal inQty, decimal inCost)
    {
        if (inQty <= 0)
        {
            return oldCost;
        }

        if (oldQty <= 0)
        {
            return inCost;
        }

        var totalValue = (oldQty * oldCost) + (inQty * inCost);
        var newQty = oldQty + inQty;
        return newQty > 0 ? Math.Round(totalValue / newQty, 4, MidpointRounding.AwayFromZero) : inCost;
    }

    private static async Task<StockRow?> GetStockRowAsync(
        DbConnection connection,
        long warehouseId,
        long itemId,
        string batchNo,
        string variant,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`, `qty_on_hand`, `avg_unit_cost` FROM `epc_erp_inv_stock` WHERE `warehouse_id` = ? AND `item_id` = ? AND IFNULL(`batch_no`,'') = ? AND IFNULL(`variant_label`,'') = ? LIMIT 1");
        ErpDb.AddParameters(cmd, warehouseId, itemId, batchNo, variant);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new StockRow(
            reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
            reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture));
    }

    private static async Task RegisterSerialAsync(
        DbConnection connection,
        long itemId,
        string serial,
        long warehouseId,
        string batchNo,
        decimal unitCost,
        long movementId,
        bool incoming,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (incoming)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_erp_inv_serials`
                    (`item_id`,`serial_no`,`warehouse_id`,`batch_no`,`status`,`in_movement_id`,`unit_cost`,`time_created`,`time_updated`)
                    VALUES (?,?,?,?, 'in_stock', ?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE `status`='in_stock', `warehouse_id`=VALUES(`warehouse_id`),
                      `batch_no`=VALUES(`batch_no`), `in_movement_id`=VALUES(`in_movement_id`),
                      `unit_cost`=VALUES(`unit_cost`), `time_updated`=VALUES(`time_updated`)
                    """),
                cancellationToken,
                itemId,
                serial,
                warehouseId,
                batchNo.Length == 0 ? null : batchNo,
                movementId,
                unitCost,
                now,
                now);
            return;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_inv_serials` SET `status`='sold', `out_movement_id`=?, `time_updated`=? WHERE `item_id`=? AND `serial_no`=?"),
            cancellationToken,
            movementId,
            now,
            itemId,
            serial);
    }

    private static async Task<bool> HasColumnAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    private static long ParseMovementDate(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && DateTime.TryParse(raw.Trim() + " 12:00:00", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return new DateTimeOffset(parsed).ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private sealed record StockRow(long Id, decimal QtyOnHand, decimal AvgUnitCost);
}
