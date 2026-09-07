using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rfid_register_tag</c> twin. Schema-ensure and scan sessions stay PHP.
/// </summary>
public interface IErpRfidRegisterWriteService
{
    Task<ErpSimpleWriteResult> RegisterAsync(
        ErpRfidRegisterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRfidRegisterRequest(
    int CompanyId = 0,
    string? RfidEpc = null,
    string? RfidTid = null,
    long ProductId = 0,
    string? Barcode = null,
    string? Sku = null,
    string? ItemDescription = null,
    long WarehouseId = 0,
    string? LocationZone = null,
    long RegisteredBy = 0);

public sealed class ErpRfidRegisterWriteService : IErpRfidRegisterWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRfidRegisterWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RegisterAsync(
        ErpRfidRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var epc = Clip((request.RfidEpc ?? string.Empty).Trim(), 100);
        if (epc.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFID EPC is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_rfid_tags", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rfid_tags", "rfid_epc", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFID tables are not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rfid_tags` (`company_id`,`rfid_epc`,`rfid_tid`,`product_id`,`barcode`,`sku`,`item_description`,`warehouse_id`,`location_zone`,`status`,`registered_by`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            epc,
            Clip((request.RfidTid ?? string.Empty).Trim(), 100),
            request.ProductId < 0 ? 0 : request.ProductId,
            Clip((request.Barcode ?? string.Empty).Trim(), 100),
            Clip((request.Sku ?? string.Empty).Trim(), 100),
            Clip((request.ItemDescription ?? string.Empty).Trim(), 300),
            request.WarehouseId < 0 ? 0 : request.WarehouseId,
            Clip((request.LocationZone ?? string.Empty).Trim(), 50),
            "active",
            request.RegisteredBy < 0 ? 0 : request.RegisteredBy,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("RFID tag registered", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
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

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
