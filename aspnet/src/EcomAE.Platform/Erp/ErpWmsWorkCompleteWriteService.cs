using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wms_work_complete</c> twin. Applies LP stock by work type and closes the line.
/// Schema ensure stays PHP. Does not CREATE tables.
/// </summary>
public interface IErpWmsWorkCompleteWriteService
{
    Task<ErpSimpleWriteResult> CompleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpWmsWorkCompleteWriteService : IErpWmsWorkCompleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsWorkCompleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CompleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "id must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_erp_wms_work", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_wms_work", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_wms_work", "work_type", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_erp_wms_lp", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_wms_lp", "qty", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "WMS work tables are not provisioned");
        }

        var wavesReady = await TableExistsAsync(connection, "epc_erp_wms_waves", cancellationToken).ConfigureAwait(false)
                         && await ColumnExistsAsync(connection, "epc_erp_wms_waves", "status", cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var work = await LoadWorkAsync(connection, transaction, id, cancellationToken).ConfigureAwait(false);
            if (work is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Work not found");
            }

            if (string.Equals(work.Status, "closed", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Work already closed");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lpId = work.LpId;
            try
            {
                lpId = await ApplyStockAsync(connection, transaction, work, now, cancellationToken).ConfigureAwait(false);
            }
            catch (ErpWriteException ex)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", ex.Message);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_wms_work` SET `status`='closed', `lp_id`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                lpId,
                now,
                id).ConfigureAwait(false);

            if (wavesReady && work.WaveId > 0)
            {
                await MaybeCloseWaveAsync(connection, transaction, work.WaveId, now, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Work completed", id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<long> ApplyStockAsync(
        DbConnection connection,
        DbTransaction transaction,
        WorkRow work,
        long now,
        CancellationToken cancellationToken)
    {
        var lpId = work.LpId;
        if (string.Equals(work.WorkType, "putaway", StringComparison.Ordinal))
        {
            if (lpId > 0)
            {
                await MoveLpAsync(connection, transaction, lpId, work.ToLocationId, now, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                lpId = await UpsertLpAsync(
                    connection, transaction, work.CompanyId, string.Empty, work.ToLocationId, work.Item, work.Qty, now, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else if (string.Equals(work.WorkType, "pick", StringComparison.Ordinal))
        {
            if (lpId <= 0)
            {
                lpId = work.FromLocationId > 0
                    ? await ErpDb.LongAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "SELECT `id` FROM `epc_erp_wms_lp` WHERE `company_id`=? AND `item`=? AND `status`='active' AND `qty`>=? AND `location_id`=? ORDER BY `qty` ASC LIMIT 1"),
                        cancellationToken,
                        work.CompanyId, work.Item, work.Qty, work.FromLocationId).ConfigureAwait(false)
                    : await ErpDb.LongAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "SELECT `id` FROM `epc_erp_wms_lp` WHERE `company_id`=? AND `item`=? AND `status`='active' AND `qty`>=? ORDER BY `qty` ASC LIMIT 1"),
                        cancellationToken,
                        work.CompanyId, work.Item, work.Qty).ConfigureAwait(false);
            }

            if (lpId <= 0)
            {
                throw new ErpWriteException("No license plate with enough stock to pick");
            }

            await AdjustLpAsync(connection, transaction, lpId, -work.Qty, now, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(work.WorkType, "move", StringComparison.Ordinal))
        {
            if (lpId > 0)
            {
                await MoveLpAsync(connection, transaction, lpId, work.ToLocationId, now, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (string.Equals(work.WorkType, "count", StringComparison.Ordinal) && lpId > 0)
        {
            var existingQty = await ErpDb.DecimalAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `qty` FROM `epc_erp_wms_lp` WHERE `id`=?"),
                cancellationToken,
                lpId).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_wms_lp` WHERE `id`=?"),
                cancellationToken,
                lpId).ConfigureAwait(false);
            if (exists > 0)
            {
                await AdjustLpAsync(connection, transaction, lpId, work.Qty - existingQty, now, cancellationToken).ConfigureAwait(false);
            }
        }

        return lpId;
    }

    private static async Task MoveLpAsync(
        DbConnection connection,
        DbTransaction transaction,
        long lpId,
        long toLocationId,
        long now,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `epc_erp_wms_lp` SET `location_id`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            toLocationId,
            now,
            lpId).ConfigureAwait(false);
    }

    private static async Task AdjustLpAsync(
        DbConnection connection,
        DbTransaction transaction,
        long lpId,
        decimal delta,
        long now,
        CancellationToken cancellationToken)
    {
        var qty = await ErpDb.DecimalAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `qty` FROM `epc_erp_wms_lp` WHERE `id`=?"),
            cancellationToken,
            lpId).ConfigureAwait(false);
        var newQty = Math.Round(qty + delta, 3, MidpointRounding.AwayFromZero);
        if (newQty < 0)
        {
            throw new ErpWriteException("Insufficient quantity on license plate");
        }

        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `epc_erp_wms_lp` SET `qty`=?, `status`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            newQty,
            newQty > 0 ? "active" : "closed",
            now,
            lpId).ConfigureAwait(false);
    }

    private static async Task<long> UpsertLpAsync(
        DbConnection connection,
        DbTransaction transaction,
        long companyId,
        string? lpCode,
        long locationId,
        string item,
        decimal qty,
        long now,
        CancellationToken cancellationToken)
    {
        var code = (lpCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            var seq = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE `company_id`=?"),
                cancellationToken,
                companyId).ConfigureAwait(false) + 1;
            code = ErpWmsReceiveWriteService.FormatAutoLpCode(seq);
        }

        var existingId = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_wms_lp` WHERE `company_id`=? AND `lp_code`=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        if (existingId > 0)
        {
            var existingQty = await ErpDb.DecimalAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `qty` FROM `epc_erp_wms_lp` WHERE `id`=?"),
                cancellationToken,
                existingId).ConfigureAwait(false);
            var existingItem = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `item` FROM `epc_erp_wms_lp` WHERE `id`=?"),
                cancellationToken,
                existingId).ConfigureAwait(false);
            var newQty = existingQty + qty;
            var storedItem = item.Length > 0 ? item : (existingItem ?? string.Empty);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_wms_lp` SET `location_id`=?, `item`=?, `qty`=?, `status`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                locationId,
                storedItem,
                newQty,
                newQty > 0 ? "active" : "closed",
                now,
                existingId);
            return existingId;
        }

        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_wms_lp` (`company_id`,`lp_code`,`location_id`,`item`,`qty`,`status`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            code,
            locationId,
            item,
            qty,
            qty > 0 ? "active" : "closed",
            now,
            now);
        return await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task MaybeCloseWaveAsync(
        DbConnection connection,
        DbTransaction transaction,
        long waveId,
        long now,
        CancellationToken cancellationToken)
    {
        var open = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE `wave_id`=? AND `status`<>'closed'"),
            cancellationToken,
            waveId).ConfigureAwait(false);
        var total = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE `wave_id`=?"),
            cancellationToken,
            waveId).ConfigureAwait(false);
        if (total > 0 && open == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_wms_waves` SET `status`='closed', `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                now,
                waveId).ConfigureAwait(false);
        }
    }

    private static async Task<WorkRow?> LoadWorkAsync(
        DbConnection connection,
        DbTransaction transaction,
        long id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `company_id`,`work_type`,`qty`,`lp_id`,`from_location_id`,`to_location_id`,`item`,`status`,`wave_id` FROM `epc_erp_wms_work` WHERE `id`=?");
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new WorkRow(
            CompanyId: reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            WorkType: reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
            Qty: reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
            LpId: reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
            FromLocationId: reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
            ToLocationId: reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
            Item: reader.IsDBNull(6) ? string.Empty : Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty,
            Status: reader.IsDBNull(7) ? string.Empty : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty,
            WaveId: reader.IsDBNull(8) ? 0 : Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture));
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

    private sealed record WorkRow(
        long CompanyId,
        string WorkType,
        decimal Qty,
        long LpId,
        long FromLocationId,
        long ToLocationId,
        string Item,
        string Status,
        long WaveId);
}
