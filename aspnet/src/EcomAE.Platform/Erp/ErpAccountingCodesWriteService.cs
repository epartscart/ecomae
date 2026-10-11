using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>operations_editor.php</c> / <c>operation_create_edit.php</c> twin.
/// Lists <c>shop_accounting_codes</c> with used counts, inserts/updates
/// (<c>income</c>, <c>name</c>, <c>manual_available</c>), deletes with the PHP
/// guards: refuse <c>system=1</c> rows and codes used in
/// <c>shop_users_accounting.operation_code</c>. Does not CREATE tables.
/// </summary>
public interface IErpAccountingCodesService
{
    Task<IReadOnlyList<ErpAccountingCodeRow>> ListAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        ErpAccountingCodeSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed record ErpAccountingCodeRow(
    long Id,
    int Income,
    string Name,
    int ManualAvailable,
    int System,
    string? Key,
    long Used);

public sealed record ErpAccountingCodeSaveRequest(
    long Id = 0,
    int Income = 0,
    string? Name = null,
    int? ManualAvailable = null,
    int? System = null);

public sealed class ErpAccountingCodesService : IErpAccountingCodesService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAccountingCodesService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpAccountingCodeRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "shop_accounting_codes", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var hasUsed = await TableExistsAsync(connection, "shop_users_accounting", cancellationToken).ConfigureAwait(false);
        var systemExpr = await ColumnExistsAsync(connection, "shop_accounting_codes", "system", cancellationToken).ConfigureAwait(false)
            ? "`system`"
            : "0 AS `system`";
        var usedExpr = hasUsed
            ? "(SELECT COUNT(*) FROM `shop_users_accounting` WHERE `operation_code` = `shop_accounting_codes`.`id`) AS `used`"
            : "0 AS `used`";
        var sql = ErpDb.Positional(
            "SELECT `id`,`income`,`name`,`manual_available`," + systemExpr + ",`key`," + usedExpr
            + " FROM `shop_accounting_codes` ORDER BY `id` LIMIT 500");

        var rows = new List<ErpAccountingCodeRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpAccountingCodeRow(
                Convert.ToInt64(reader.GetValue(0)),
                Convert.ToInt32(reader.GetValue(1)),
                Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                Convert.ToInt32(reader.GetValue(3)),
                Convert.ToInt32(reader.GetValue(4)),
                reader.IsDBNull(5) ? null : Convert.ToString(reader.GetValue(5)),
                Convert.ToInt64(reader.GetValue(6))));
        }

        return rows;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpAccountingCodeSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        name = name.Length <= 255 ? name : name[..255];
        var income = request.Income == 1 ? 1 : 0;
        var manualAvailable = request.ManualAvailable is null ? 1 : (request.ManualAvailable.Value == 1 ? 1 : 0);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "shop_accounting_codes", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Accounting codes table is not provisioned");
        }

        if (request.Id > 0)
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_accounting_codes` WHERE `id`=?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);
            if (exists == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Operation not found");
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_accounting_codes` SET `income`=?, `name`=? WHERE `id`=?"),
                cancellationToken,
                income,
                name,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Operation saved", request.Id);
        }

        var hasSystem = await ColumnExistsAsync(connection, "shop_accounting_codes", "system", cancellationToken).ConfigureAwait(false);
        if (hasSystem)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_accounting_codes` (`income`,`name`,`manual_available`,`system`) VALUES (?,?,?,?)"),
                cancellationToken,
                income,
                name,
                manualAvailable,
                request.System is null ? 0 : (request.System.Value == 1 ? 1 : 0)).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_accounting_codes` (`income`,`name`,`manual_available`) VALUES (?,?,?)"),
                cancellationToken,
                income,
                name,
                manualAvailable).ConfigureAwait(false);
        }
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Operation saved", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Operation not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "shop_accounting_codes", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Accounting codes table is not provisioned");
        }

        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_accounting_codes` WHERE `id`=?"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (exists == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Operation not found");
        }

        var hasSystem = await ColumnExistsAsync(connection, "shop_accounting_codes", "system", cancellationToken).ConfigureAwait(false);
        var system = hasSystem
            ? await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `system` FROM `shop_accounting_codes` WHERE `id`=?"),
                cancellationToken,
                id).ConfigureAwait(false)
            : 0;
        if (system == 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "System operations can not be deleted");
        }

        if (await TableExistsAsync(connection, "shop_users_accounting", cancellationToken).ConfigureAwait(false))
        {
            var used = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_users_accounting` WHERE `operation_code`=?"),
                cancellationToken,
                id).ConfigureAwait(false);
            if (used > 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Operation is used and can not be deleted");
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_accounting_codes` WHERE `id`=?"),
            cancellationToken,
            id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Operation deleted", id);
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
}
