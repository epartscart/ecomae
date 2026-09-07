using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rfid_start_scan_session</c> / <c>epc_rfid_process_scan</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpRfidScanWriteService
{
    Task<ErpSimpleWriteResult> StartSessionAsync(
        ErpRfidStartSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ProcessScanAsync(
        ErpRfidProcessScanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRfidStartSessionRequest(
    int CompanyId = 0,
    string? SessionType = null,
    long WarehouseId = 0,
    string? Zone = null,
    long ScannedBy = 0,
    string? ScannedByName = null);

public sealed record ErpRfidProcessScanRequest(
    long SessionId = 0,
    int CompanyId = 0,
    string? RfidEpc = null,
    int Rssi = 0);

public sealed class ErpRfidScanWriteService : IErpRfidScanWriteService
{
    private static readonly HashSet<string> SessionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "stocktake", "audit", "gate_check", "search"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpRfidScanWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> StartSessionAsync(
        ErpRfidStartSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_rfid_scan_sessions", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rfid_scan_sessions", "session_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFID scan tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var warehouseId = request.WarehouseId < 0 ? 0 : request.WarehouseId;
        var expected = 0L;
        if (warehouseId > 0
            && await TableExistsAsync(connection, "epc_rfid_tags", cancellationToken).ConfigureAwait(false)
            && await ColumnExistsAsync(connection, "epc_rfid_tags", "status", cancellationToken).ConfigureAwait(false))
        {
            expected = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_rfid_tags` WHERE `company_id` = ? AND `warehouse_id` = ? AND `status` = 'active'"),
                cancellationToken,
                companyId,
                warehouseId).ConfigureAwait(false);
        }

        var sessionType = (request.SessionType ?? string.Empty).Trim().ToLowerInvariant();
        if (!SessionTypes.Contains(sessionType))
        {
            sessionType = "stocktake";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rfid_scan_sessions` (`company_id`,`session_type`,`warehouse_id`,`zone`,`total_expected`,`scanned_by`,`scanned_by_name`,`status`,`time_started`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            sessionType,
            warehouseId,
            Clip((request.Zone ?? string.Empty).Trim(), 50),
            expected,
            request.ScannedBy < 0 ? 0 : request.ScannedBy,
            Clip((request.ScannedByName ?? string.Empty).Trim(), 120),
            "in_progress",
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("RFID scan session started", id);
    }

    public async Task<ErpSimpleWriteResult> ProcessScanAsync(
        ErpRfidProcessScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Session id is required.");
        }

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
        var sessionsOk = await TableExistsAsync(connection, "epc_rfid_scan_sessions", cancellationToken).ConfigureAwait(false)
                         && await ColumnExistsAsync(connection, "epc_rfid_scan_sessions", "total_scanned", cancellationToken).ConfigureAwait(false);
        var resultsOk = await TableExistsAsync(connection, "epc_rfid_scan_results", cancellationToken).ConfigureAwait(false)
                        && await ColumnExistsAsync(connection, "epc_rfid_scan_results", "rfid_epc", cancellationToken).ConfigureAwait(false);
        var tagsOk = await TableExistsAsync(connection, "epc_rfid_tags", cancellationToken).ConfigureAwait(false)
                     && await ColumnExistsAsync(connection, "epc_rfid_tags", "rfid_epc", cancellationToken).ConfigureAwait(false);
        if (!sessionsOk || !resultsOk || !tagsOk)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFID scan tables are not provisioned");
        }

        var sessionExists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_rfid_scan_sessions` WHERE `id` = ?"),
            cancellationToken,
            request.SessionId).ConfigureAwait(false);
        if (sessionExists <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Scan session not found");
        }

        var tagId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_rfid_tags` WHERE `company_id` = ? AND `rfid_epc` = ? LIMIT 1"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            epc).ConfigureAwait(false);
        var result = tagId > 0 ? "found" : "unexpected";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_rfid_scan_results` (`session_id`,`rfid_epc`,`tag_id`,`scan_result`,`signal_strength`,`time_scanned`) VALUES (?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.SessionId,
            epc,
            tagId,
            result,
            request.Rssi,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (tagId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_rfid_tags` SET `last_scanned_at` = NOW(), `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                UnixNow(),
                tagId).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `epc_rfid_scan_sessions` SET `total_scanned` = `total_scanned` + 1, `total_found` = `total_found` + ? WHERE `id` = ?"),
            cancellationToken,
            tagId > 0 ? 1 : 0,
            request.SessionId).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("RFID scan " + result, id);
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
