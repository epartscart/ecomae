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

    Task<ErpAccountOperationsPage?> ListOperationsAsync(
        ErpAccountOperationsFilter filter,
        string sortField,
        bool ascending,
        int rowFrom,
        int rowsPerPage,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        ErpAccountingCodeSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>PHP create_operation.php twin: manual balance entry for a user.</summary>
    Task<ErpSimpleWriteResult> CreateUserOperationAsync(
        ErpUserOperationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpUserOperationRequest(
    long UserId = 0,
    int Income = 0,
    long OperationCode = 0,
    decimal Amount = 0,
    long OfficeId = 0);

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

/// <summary>One ledger row of <c>shop_users_accounting</c> as rendered by PHP account_operations.php.</summary>
public sealed record ErpAccountOperationRow(
    long Id,
    long Time,
    decimal Amount,
    int Income,
    long OrderId,
    long UserId,
    string Customer,
    long OperationCode,
    string OperationName,
    string OfficeCaption);

public sealed record ErpAccountOperationsFilter(
    long TimeFrom = 0,
    long TimeTo = 0,
    int Income = -1,
    long OperationCode = -1,
    long? UserId = null,
    long? OrderId = null,
    long? OfficeId = null);

public sealed record ErpAccountOperationsPage(
    IReadOnlyList<ErpAccountOperationRow> Rows,
    long TotalRows,
    decimal Balance);

public static class ErpAccountingCodesTable
{
    public static async Task<bool> ReadyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            "shop_accounting_codes").ConfigureAwait(false);
        return n > 0;
    }
}

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

    /// <summary>
    /// Ledger read for PHP account_operations.php: filtered/sorted/paginated rows of
    /// <c>shop_users_accounting</c> plus the running balance (income sum − expense sum)
    /// computed over the same filters minus the income condition.
    /// </summary>
    public async Task<ErpAccountOperationsPage?> ListOperationsAsync(
        ErpAccountOperationsFilter filter,
        string sortField,
        bool ascending,
        int rowFrom,
        int rowsPerPage,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        var sortable = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "time", "user_id", "amount", "operation_code", "income", "order_id", "office_caption",
        };
        if (!sortable.Contains(sortField))
        {
            sortField = "id";
        }
        var direction = ascending ? "ASC" : "DESC";

        var conditions = new List<string> { "`active` = 1" };
        var balanceConditions = new List<string> { "`active` = 1" };
        var bindings = new List<object?>();
        var balanceBindings = new List<object?>();

        if (filter.TimeFrom > 0)
        {
            conditions.Add("`time` > ?");
            bindings.Add(filter.TimeFrom);
            balanceConditions.Add("`time` > ?");
            balanceBindings.Add(filter.TimeFrom);
        }
        if (filter.TimeTo > 0)
        {
            conditions.Add("`time` < ?");
            bindings.Add(filter.TimeTo);
            balanceConditions.Add("`time` < ?");
            balanceBindings.Add(filter.TimeTo);
        }
        if (filter.Income == 0 || filter.Income == 1)
        {
            conditions.Add("`income` = ?");
            bindings.Add(filter.Income);
        }
        if (filter.OperationCode > 0)
        {
            conditions.Add("`operation_code` = ?");
            bindings.Add(filter.OperationCode);
            balanceConditions.Add("`operation_code` = ?");
            balanceBindings.Add(filter.OperationCode);
        }
        if (filter.UserId is not null)
        {
            conditions.Add("`user_id` = ?");
            bindings.Add(filter.UserId.Value);
            balanceConditions.Add("`user_id` = ?");
            balanceBindings.Add(filter.UserId.Value);
        }
        if (filter.OrderId is not null)
        {
            conditions.Add("`order_id` = ?");
            bindings.Add(filter.OrderId.Value);
            balanceConditions.Add("`order_id` = ?");
            balanceBindings.Add(filter.OrderId.Value);
        }
        if (filter.OfficeId is not null && filter.OfficeId.Value > 0)
        {
            conditions.Add("`office_id` = ?");
            bindings.Add(filter.OfficeId.Value);
            balanceConditions.Add("`office_id` = ?");
            balanceBindings.Add(filter.OfficeId.Value);
        }

        var where = "WHERE " + string.Join(" AND ", conditions);
        var whereBalance = "WHERE " + string.Join(" AND ", balanceConditions);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "shop_users_accounting", cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var hasUsers = await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false);
        var hasCodes = await TableExistsAsync(connection, "shop_accounting_codes", cancellationToken).ConfigureAwait(false);
        var hasOffices = await TableExistsAsync(connection, "shop_offices", cancellationToken).ConfigureAwait(false);

        var balanceSql = ErpDb.Positional(
            "SELECT IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` " + whereBalance + " AND `income` = 1), 0)"
            + " - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` " + whereBalance + " AND `income` = 0), 0)");
        var balanceParams = balanceBindings.Concat(balanceBindings).ToArray();
        var balance = await ErpDb.DecimalAsync(connection, null, balanceSql, cancellationToken, balanceParams).ConfigureAwait(false);

        var opNameExpr = hasCodes
            ? "(SELECT `name` FROM `shop_accounting_codes` WHERE `id` = `shop_users_accounting`.`operation_code`)"
            : "''";
        var customerExpr = hasUsers
            ? "CONCAT('ID ', `user_id`, ', E-mail: ', IFNULL((SELECT `email` FROM `users` WHERE `user_id` = `shop_users_accounting`.`user_id` LIMIT 1), ''), ', Phone: ', IFNULL((SELECT `phone` FROM `users` WHERE `user_id` = `shop_users_accounting`.`user_id` LIMIT 1), ''))"
            : "CONCAT('ID ', `user_id`)";
        var officeExpr = hasOffices
            ? "IFNULL((SELECT CONCAT(`caption`, ', ', `city`, ', ', `address`) FROM `shop_offices` WHERE `id` = `shop_users_accounting`.`office_id`), '')"
            : "''";
        var orderExpr = sortField == "office_caption" ? officeExpr : "`" + sortField + "`";

        var listSql = "SELECT `id`,`time`,CAST(`amount` AS DECIMAL(14,2)),`income`,`order_id`,`user_id`,`operation_code`,"
            + opNameExpr + " AS `operation_name`," + customerExpr + " AS `customer`," + officeExpr + " AS `office_caption`"
            + " FROM `shop_users_accounting` " + where
            + " ORDER BY " + orderExpr + " " + direction
            + " LIMIT " + Math.Max(0, rowFrom) + "," + Math.Max(1, rowsPerPage);

        var rows = new List<ErpAccountOperationRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(listSql);
            foreach (var binding in bindings)
            {
                var p = command.CreateParameter();
                p.Value = binding ?? (object)DBNull.Value;
                command.Parameters.Add(p);
            }
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpAccountOperationRow(
                    Convert.ToInt64(reader.GetValue(0)),
                    Convert.ToInt64(reader.GetValue(1)),
                    Convert.ToDecimal(reader.GetValue(2)),
                    Convert.ToInt32(reader.GetValue(3)),
                    Convert.ToInt64(reader.GetValue(4)),
                    Convert.ToInt64(reader.GetValue(5)),
                    reader.IsDBNull(8) ? string.Empty : (Convert.ToString(reader.GetValue(8)) ?? string.Empty),
                    Convert.ToInt64(reader.GetValue(6)),
                    reader.IsDBNull(7) ? string.Empty : (Convert.ToString(reader.GetValue(7)) ?? string.Empty),
                    reader.IsDBNull(9) ? string.Empty : (Convert.ToString(reader.GetValue(9)) ?? string.Empty)));
            }
        }

        var totalSql = ErpDb.Positional("SELECT COUNT(*) FROM `shop_users_accounting` " + where);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = totalSql;
            foreach (var binding in bindings)
            {
                var p = command.CreateParameter();
                p.Value = binding ?? (object)DBNull.Value;
                command.Parameters.Add(p);
            }
            var totalObj = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var total = totalObj is null || totalObj is DBNull ? 0L : Convert.ToInt64(totalObj);
            return new ErpAccountOperationsPage(rows, total, balance);
        }
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

    /// <summary>
    /// PHP create_operation.php action=create: validates user (0 allowed), income 0/1,
    /// operation_code existing + manual_available + matching income, amount &gt; 0,
    /// optional office existence check, then INSERTs into <c>shop_users_accounting</c>.
    /// </summary>
    public async Task<ErpSimpleWriteResult> CreateUserOperationAsync(
        ErpUserOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Income != 0 && request.Income != 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Income must be 0 or 1");
        }
        if (request.Amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Amount must be a number greater than 0");
        }
        if (request.OperationCode <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Operation code is required");
        }
        if (request.UserId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid user id");
        }
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "shop_users_accounting", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "shop_accounting_codes", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Accounting tables are not provisioned");
        }

        if (request.UserId != 0 && await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false))
        {
            var userExists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `user_id` = ?"),
                cancellationToken,
                request.UserId).ConfigureAwait(false);
            if (userExists == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "User does not exist");
            }
        }

        var codeIncome = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `income` FROM `shop_accounting_codes` WHERE `id` = ?"),
            cancellationToken,
            request.OperationCode).ConfigureAwait(false);
        var codeManual = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `manual_available` FROM `shop_accounting_codes` WHERE `id` = ?"),
            cancellationToken,
            request.OperationCode).ConfigureAwait(false);
        var codeExists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_accounting_codes` WHERE `id` = ?"),
            cancellationToken,
            request.OperationCode).ConfigureAwait(false);
        if (codeExists == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Operation code does not exist");
        }
        if (codeManual != 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Operation is not available for manual entry");
        }
        if (codeIncome != request.Income)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Operation income direction does not match");
        }

        var officeId = request.OfficeId < 0 ? 0 : request.OfficeId;
        if (officeId > 0 && await TableExistsAsync(connection, "shop_offices", cancellationToken).ConfigureAwait(false))
        {
            var officeExists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ?"),
                cancellationToken,
                officeId).ConfigureAwait(false);
            if (officeExists == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Office does not exist");
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `office_id`) VALUES (?,?,?, CAST(? AS DECIMAL(14,2)),?,?,?)"),
            cancellationToken,
            request.UserId,
            now,
            request.Income,
            request.Amount,
            request.OperationCode,
            1,
            officeId).ConfigureAwait(false);
        var newId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Operation created", newId);
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
