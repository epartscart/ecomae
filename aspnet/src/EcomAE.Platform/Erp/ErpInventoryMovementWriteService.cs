using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>inv_record_movement</c> / <c>inv_transfer</c> / <c>inv_create_warehouse</c> /
/// <c>inv_create_item</c> / <c>inv_sync_warehouses</c> / <c>inv_run_closing</c> /
/// <c>inv_import_csv</c> (csv_text) twins.
/// CSV file-byte upload and dimension-link save stay Classic. Schema-ensure stays PHP.
/// </summary>
public interface IErpInventoryMovementWriteService
{
    Task<ErpSimpleWriteResult> RecordMovementAsync(
        ErpInventoryMovementWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> TransferAsync(
        ErpInventoryTransferWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateWarehouseAsync(string? code, string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SyncWarehousesAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateItemAsync(ErpInventoryItemWriteRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RunClosingAsync(string? periodEnd, long warehouseId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ImportCsvAsync(ErpInventoryCsvImportRequest request, CancellationToken cancellationToken = default);
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

public sealed record ErpInventoryItemWriteRequest(
    string? Sku = null,
    string? Name = null,
    string? ItemType = null,
    string? Unit = null,
    string? Barcode = null,
    long ProductId = 0,
    bool TrackExpiry = false,
    decimal? ReorderLevel = null,
    IReadOnlyDictionary<string, string>? CustomFields = null);

public sealed record ErpInventoryCsvImportRequest(
    int AdminUserId = 0,
    string? CsvText = null,
    long WarehouseId = 0,
    string? DefaultMovementType = null);

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

    public async Task<ErpSimpleWriteResult> CreateWarehouseAsync(
        string? code,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var storedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var storedName = (name ?? string.Empty).Trim();
        if (storedCode.Length == 0 || storedName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse code and name required");
        }

        if (storedCode.Length > 32)
        {
            storedCode = storedCode[..32];
        }

        if (storedName.Length > 255)
        {
            storedName = storedName[..255];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            storedCode).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse code already exists");
        }

        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_inv_warehouses` (`storage_id`,`code`,`name`,`time_created`) VALUES (0,?,?,?)"),
            cancellationToken,
            storedCode,
            storedName,
            createdAt);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Warehouse created", id);
    }

    public async Task<ErpSimpleWriteResult> SyncWarehousesAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var tables = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_storages'",
            cancellationToken).ConfigureAwait(false);
        if (tables == 0)
        {
            return ErpSimpleWriteResult.Ok("Synced 0 warehouse(s) from shop storages", 0);
        }

        var pending = new List<(long StorageId, string Name)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, `name` FROM `shop_storages` WHERE `hidden` = 0 OR `hidden` IS NULL";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storageId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var storageName = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
                pending.Add((storageId, storageName));
            }
        }

        var created = 0;
        foreach (var row in pending)
        {
            var code = "WH" + row.StorageId.ToString(CultureInfo.InvariantCulture);
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `storage_id` = ? OR `code` = ? LIMIT 1"),
                cancellationToken,
                row.StorageId,
                code).ConfigureAwait(false);
            if (exists > 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_inv_warehouses` (`storage_id`,`code`,`name`,`time_created`) VALUES (?,?,?,?)"),
                cancellationToken,
                row.StorageId,
                code,
                row.Name,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            created++;
        }

        return ErpSimpleWriteResult.Ok("Synced " + created.ToString(CultureInfo.InvariantCulture) + " warehouse(s) from shop storages", created);
    }

    public async Task<ErpSimpleWriteResult> CreateItemAsync(
        ErpInventoryItemWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var sku = (request.Sku ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (sku.Length == 0 || name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "SKU and name required");
        }

        var itemType = request.ItemType is "perishable" or "serialized" ? request.ItemType : "standard";
        var trackExpiry = request.TrackExpiry || itemType == "perishable" ? 1 : 0;
        var unit = (request.Unit ?? "pcs").Trim();
        if (unit.Length == 0)
        {
            unit = "pcs";
        }

        if (unit.Length > 16)
        {
            unit = unit[..16];
        }

        if (sku.Length > 64)
        {
            sku = sku[..64];
        }

        if (name.Length > 255)
        {
            name = name[..255];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` = ? LIMIT 1"),
            cancellationToken,
            sku).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "SKU already exists");
        }

        var cols = new List<string> { "sku", "name", "product_id", "item_type", "track_expiry", "unit", "time_created" };
        var vals = new List<object?>
        {
            sku, name, request.ProductId, itemType, trackExpiry, unit, DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        if (!string.IsNullOrWhiteSpace(request.Barcode) && await HasColumnAsync(connection, "epc_erp_inv_items", "barcode", cancellationToken).ConfigureAwait(false))
        {
            var barcode = request.Barcode.Trim();
            cols.Add("barcode");
            vals.Add(barcode.Length > 128 ? barcode[..128] : barcode);
        }

        if (request.ReorderLevel is { } reorder
            && await HasColumnAsync(connection, "epc_erp_inv_items", "reorder_level", cancellationToken).ConfigureAwait(false))
        {
            cols.Add("reorder_level");
            vals.Add(Math.Max(0, reorder));
        }

        var placeholders = string.Join(",", Enumerable.Range(0, cols.Count).Select(_ => "?"));
        var colList = "`" + string.Join("`,`", cols) + "`";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_inv_items` (" + colList + ") VALUES (" + placeholders + ")"),
            cancellationToken,
            vals.ToArray());
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        if (request.CustomFields is { Count: > 0 })
        {
            foreach (var pair in request.CustomFields)
            {
                var key = SanitizeFieldKey(pair.Key);
                if (key.Length == 0)
                {
                    continue;
                }

                var value = pair.Value ?? "";
                if (value.Length > 512)
                {
                    value = value[..512];
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `epc_erp_inv_item_fields` (`item_id`,`field_key`,`value`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)"),
                    cancellationToken,
                    id,
                    key,
                    value);
            }
        }

        return ErpSimpleWriteResult.Ok("Inventory item created", id);
    }

    public async Task<ErpSimpleWriteResult> RunClosingAsync(
        string? periodEnd,
        long warehouseId,
        CancellationToken cancellationToken = default)
    {
        var period = NormalizePeriodEnd(periodEnd);
        if (period.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Closing period is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sql = """
            SELECT s.`warehouse_id`, s.`item_id`, s.`qty_on_hand`, s.`avg_unit_cost`
            FROM `epc_erp_inv_stock` s
            INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id`
            INNER JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id`
            WHERE i.`active` = 1
            """;
        var args = new List<object?>();
        if (warehouseId > 0)
        {
            sql += " AND s.`warehouse_id` = ?";
            args.Add(warehouseId);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = args.Count == 0 ? sql : ErpDb.Positional(sql);
        if (args.Count > 0)
        {
            ErpDb.AddParameters(cmd, args.ToArray());
        }

        var rows = new List<(long WarehouseId, long ItemId, decimal Qty, decimal Avg)>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture)));
            }
        }

        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in rows)
        {
            var value = Math.Round(row.Qty * row.Avg, 2, MidpointRounding.AwayFromZero);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_erp_inv_closing`
                    (`period_end`,`warehouse_id`,`item_id`,`qty_closing`,`avg_unit_cost`,`value_closing`,`time_created`)
                    VALUES (?,?,?,?,?,?,?)
                    ON DUPLICATE KEY UPDATE `qty_closing` = VALUES(`qty_closing`), `avg_unit_cost` = VALUES(`avg_unit_cost`), `value_closing` = VALUES(`value_closing`)
                    """),
                cancellationToken,
                period,
                row.WarehouseId,
                row.ItemId,
                row.Qty,
                row.Avg,
                value,
                createdAt);
        }

        return ErpSimpleWriteResult.Ok("Closing snapshot saved for " + rows.Count.ToString(CultureInfo.InvariantCulture) + " line(s)", rows.Count);
    }

    public async Task<ErpSimpleWriteResult> ImportCsvAsync(
        ErpInventoryCsvImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var defaultType = string.IsNullOrWhiteSpace(request.DefaultMovementType) ? "purchase_in" : request.DefaultMovementType.Trim();
        var parsed = ParseCsvText(request.CsvText, request.WarehouseId, defaultType);
        if (parsed.Count == 0)
        {
            return ErpSimpleWriteResult.Ok("Posted 0 movement(s)", 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var posted = 0;
        var errors = new List<string>();
        for (var i = 0; i < parsed.Count; i++)
        {
            var row = parsed[i];
            var lineNo = i + 2;
            try
            {
                var warehouseId = await ResolveWarehouseAsync(
                    connection,
                    ParseLong(row, "warehouse_id"),
                    Row(row, "warehouse_code"),
                    cancellationToken).ConfigureAwait(false);
                if (warehouseId <= 0)
                {
                    throw new ErpWriteException("Warehouse required");
                }

                var itemId = ParseLong(row, "item_id");
                if (itemId <= 0)
                {
                    itemId = await ResolveItemIdBySkuAsync(connection, Row(row, "sku"), cancellationToken).ConfigureAwait(false);
                    if (itemId <= 0)
                    {
                        throw new ErpWriteException("Unknown SKU: " + Row(row, "sku"));
                    }
                }

                var qty = ParseDec(row, "qty");
                if (qty == 0)
                {
                    throw new ErpWriteException("Qty is zero");
                }

                var type = Row(row, "movement_type");
                if (type.Length == 0)
                {
                    type = defaultType;
                }

                if (type == "transfer")
                {
                    var toWh = await ResolveWarehouseAsync(connection, 0, Row(row, "to_warehouse_code"), cancellationToken)
                        .ConfigureAwait(false);
                    if (toWh <= 0)
                    {
                        toWh = ParseLong(row, "to_warehouse_id");
                    }

                    if (toWh <= 0)
                    {
                        throw new ErpWriteException("transfer requires to_warehouse_code or to_warehouse_id");
                    }

                    var reference = Row(row, "reference");
                    if (reference.Length == 0)
                    {
                        reference = "CSV-TRF";
                    }

                    await TransferCoreAsync(
                        connection,
                        new ErpInventoryTransferWriteRequest(
                            request.AdminUserId,
                            warehouseId,
                            toWh,
                            itemId,
                            Math.Abs(qty),
                            Row(row, "batch_no"),
                            null,
                            reference,
                            null),
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var qtySigned = qty;
                    if (type is "opening" or "purchase_in" or "transfer_in" or "return_in" && qtySigned < 0)
                    {
                        type = "adjustment";
                    }
                    else if (type is "sale_out" or "transfer_out" or "return_out" && qtySigned < 0)
                    {
                        qtySigned = Math.Abs(qtySigned);
                    }

                    var reference = Row(row, "reference");
                    if (reference.Length == 0)
                    {
                        reference = "CSV";
                    }

                    await RecordMovementCoreAsync(
                        connection,
                        new ErpInventoryMovementWriteRequest(
                            request.AdminUserId,
                            type,
                            warehouseId,
                            itemId,
                            qtySigned,
                            ParseDec(row, "unit_cost"),
                            Row(row, "batch_no"),
                            null,
                            Row(row, "expiry_date"),
                            null,
                            reference),
                        type,
                        cancellationToken).ConfigureAwait(false);
                }

                posted++;
            }
            catch (ErpWriteException ex)
            {
                errors.Add("Line " + lineNo.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
            }
        }

        var message = "Posted " + posted.ToString(CultureInfo.InvariantCulture) + " movement(s)";
        if (errors.Count > 0)
        {
            message += "; " + errors.Count.ToString(CultureInfo.InvariantCulture) + " error(s): " + string.Join("; ", errors.Take(5));
        }

        var ok = posted > 0 || errors.Count == 0;
        return ok
            ? ErpSimpleWriteResult.Ok(message, posted)
            : ErpSimpleWriteResult.Fail("invalid", message);
    }

    public static IReadOnlyList<Dictionary<string, string>> ParseCsvText(string? csvText, long defaultWarehouseId, string defaultMovementType)
    {
        var text = (csvText ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        var rows = new List<Dictionary<string, string>>();
        if (text.Length == 0)
        {
            return rows;
        }

        string[]? header = null;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var cols = SplitCsvLine(line);
            if (header is null)
            {
                header = new string[cols.Count];
                for (var i = 0; i < cols.Count; i++)
                {
                    header[i] = SanitizeCsvHeader(cols[i]);
                }

                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["warehouse_id"] = defaultWarehouseId.ToString(CultureInfo.InvariantCulture),
                ["movement_type"] = defaultMovementType
            };
            for (var i = 0; i < header.Length; i++)
            {
                var key = header[i];
                if (key.Length == 0)
                {
                    continue;
                }

                row[key] = i < cols.Count ? cols[i].Trim() : "";
            }

            if (row.TryGetValue("sku", out var sku) && sku.Length > 0
                || row.TryGetValue("item_id", out var itemRaw) && itemRaw.Length > 0)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private async Task TransferCoreAsync(
        DbConnection connection,
        ErpInventoryTransferWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FromWarehouseId <= 0 || request.ToWarehouseId <= 0 || request.FromWarehouseId == request.ToWarehouseId)
        {
            throw new ErpWriteException("Source and destination warehouses required (must differ)");
        }

        if (request.ItemId <= 0 || request.Qty <= 0)
        {
            throw new ErpWriteException("Item and positive quantity required");
        }

        var batch = (request.BatchNo ?? string.Empty).Trim();
        var variant = (request.VariantLabel ?? string.Empty).Trim();
        var reference = (request.Reference ?? string.Empty).Trim();
        if (reference.Length == 0)
        {
            reference = "TRF-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        var row = await GetStockRowAsync(connection, request.FromWarehouseId, request.ItemId, batch, variant, cancellationToken)
            .ConfigureAwait(false);
        if (row is null || row.QtyOnHand < request.Qty)
        {
            throw new ErpWriteException("Insufficient stock at source warehouse");
        }

        var unitCost = row.AvgUnitCost;
        var note = (request.Note ?? string.Empty).Trim();
        await RecordMovementCoreAsync(
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
    }

    private static async Task<long> ResolveWarehouseAsync(
        DbConnection connection,
        long warehouseId,
        string warehouseCode,
        CancellationToken cancellationToken)
    {
        if (warehouseId > 0)
        {
            return warehouseId;
        }

        var code = warehouseCode.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return 0;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);
    }

    private static async Task<long> ResolveItemIdBySkuAsync(
        DbConnection connection,
        string sku,
        CancellationToken cancellationToken)
    {
        sku = sku.Trim();
        if (sku.Length == 0)
        {
            return 0;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            sku).ConfigureAwait(false);
    }

    private static string Row(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value ?? "" : "";

    private static long ParseLong(IReadOnlyDictionary<string, string> row, string key)
        => long.TryParse(Row(row, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static decimal ParseDec(IReadOnlyDictionary<string, string> row, string key)
        => decimal.TryParse(Row(row, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static string SanitizeCsvHeader(string raw)
    {
        var source = (raw ?? string.Empty).Trim().ToLowerInvariant();
        var chars = new char[source.Length];
        var n = 0;
        foreach (var ch in source)
        {
            chars[n++] = ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' ? ch : '_';
        }

        return n == 0 ? string.Empty : new string(chars, 0, n);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var cols = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                quoted = true;
            }
            else if (ch == ',')
            {
                cols.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cols.Add(current.ToString());
        return cols;
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

    private static string SanitizeFieldKey(string? raw)
    {
        var source = (raw ?? string.Empty).Trim().ToLowerInvariant();
        var chars = new char[source.Length];
        var n = 0;
        foreach (var ch in source)
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')
            {
                chars[n++] = ch;
            }
        }

        return n == 0 ? string.Empty : new string(chars, 0, n);
    }

    private static string NormalizePeriodEnd(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            var today = DateTime.UtcNow;
            var last = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month), 0, 0, 0, DateTimeKind.Utc);
            return last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return string.Empty;
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
