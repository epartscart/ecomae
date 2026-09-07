using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_mfg_wo_create</c> / ajax <c>mfg_wo_create</c> twin. INSERT
/// <c>epc_mfg_work_orders</c> for an existing BOM. Does not CREATE tables and
/// does not post inventory. BOM save, WO issue/complete, and schema ensure stay PHP.
/// </summary>
public interface IErpMfgWoCreateWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpMfgWoCreateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpMfgWoCreateWriteRequest(
    long BomId = 0,
    string? WoNo = null,
    decimal QtyPlanned = 0,
    long WarehouseId = 0);

public sealed class ErpMfgWoCreateWriteService : IErpMfgWoCreateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMfgWoCreateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpMfgWoCreateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BomId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "BOM not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var qty = decimal.Round(request.QtyPlanned, 4, MidpointRounding.AwayFromZero);
        var warehouseId = request.WarehouseId < 0 ? 0 : request.WarehouseId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var woNo = Clip((request.WoNo ?? string.Empty).Trim(), 40);
        if (woNo.Length == 0)
        {
            woNo = "WO-" + now.ToString(CultureInfo.InvariantCulture);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_mfg_bom", "product_item_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_mfg_work_orders", "wo_no", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_mfg_work_orders", "bom_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Manufacturing work-order tables are not provisioned");
        }

        var productItemId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `product_item_id` FROM `epc_mfg_bom` WHERE `id`=?"),
            cancellationToken,
            request.BomId).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_mfg_bom` WHERE `id`=?"),
            cancellationToken,
            request.BomId).ConfigureAwait(false);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "BOM not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_mfg_work_orders` (`wo_no`,`bom_id`,`product_item_id`,`qty_planned`,`warehouse_id`,`status`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,'planned',?,?)"),
            cancellationToken,
            woNo, request.BomId, productItemId, qty, warehouseId, now, now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(FormatCreatedMessage(woNo, inserted), inserted);
    }

    public static string FormatCreatedMessage(string? woNo, long id)
    {
        var label = string.IsNullOrWhiteSpace(woNo)
            ? "#" + id.ToString(CultureInfo.InvariantCulture)
            : woNo.Trim();
        return "Work order " + label + " created";
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
