using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>payment_systems_standart.php</c> + <c>payment_systems_wholesaler.php</c> twin.
/// Reads <c>shop_payment_systems</c> (created lazily with the epc DDL when absent —
/// it is a shop-core table provisioned by <c>epc-integrations-setup.php</c>).
/// Standart: one global active system — deactivate all, then set the chosen
/// system <c>active=1</c> + <c>parameters_values</c> (PHP's cumulative result).
/// Wholesaler: per-office — validates the office exists, the admin is one of its
/// managers (<c>shop_offices.users</c> JSON), the system exists (or 0 to clear),
/// then updates <c>shop_offices.pay_system_id</c> / <c>pay_system_parameters</c>.
/// </summary>
public interface IErpPaymentSystemsService
{
    Task<IReadOnlyList<ErpPaymentSystemRow>> ListSystemsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErpPayOfficeRow>> ListOfficesAsync(long adminUserId, CancellationToken cancellationToken = default);
    Task<bool> OfficesProvisionedAsync(CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SaveStandartAsync(long systemId, string? parametersValues, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SaveWholesalerAsync(long adminUserId, long officeId, long systemId, string? parametersValues, CancellationToken cancellationToken = default);
}

public sealed record ErpPaymentSystemRow(
    long Id,
    string Name,
    string Description,
    string Parameters,
    string ParametersValues,
    int Active);

public sealed record ErpPayOfficeRow(
    long Id,
    string Caption,
    string City,
    string Address,
    long PaySystemId,
    string PaySystemParameters);

public sealed class ErpPaymentSystemsService : IErpPaymentSystemsService
{
    private const string PaymentSystemsDdl =
        "CREATE TABLE IF NOT EXISTS `shop_payment_systems` ("
        + "`id` int(11) NOT NULL AUTO_INCREMENT,"
        + "`name` text,"
        + "`parameters` text,"
        + "`parameters_values` text,"
        + "`active` tinyint(1) DEFAULT 0,"
        + "`description` text,"
        + "`is_selectable` tinyint(1) DEFAULT 0,"
        + "`handler` varchar(255) DEFAULT NULL,"
        + "PRIMARY KEY (`id`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPaymentSystemsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    private async Task<DbConnection?> OpenAndEnsureSystemsAsync(CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }
        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, PaymentSystemsDdl, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public async Task<IReadOnlyList<ErpPaymentSystemRow>> ListSystemsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAndEnsureSystemsAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return [];
        }

        var hasAnable = await ColumnExistsAsync(connection, "shop_payment_systems", "anable", cancellationToken).ConfigureAwait(false);
        var hasSelectable = await ColumnExistsAsync(connection, "shop_payment_systems", "is_selectable", cancellationToken).ConfigureAwait(false);
        var where = hasAnable
            ? " WHERE `anable` = 1"
            : hasSelectable ? " WHERE `is_selectable` = 1" : "";
        var sql = ErpDb.Positional(
            "SELECT `id`,"
            + "COALESCE(CAST(`name` AS CHAR),'') AS `name`,"
            + "COALESCE(CAST(`description` AS CHAR),'') AS `description`,"
            + "COALESCE(CAST(`parameters` AS CHAR),'[]') AS `parameters`,"
            + "COALESCE(CAST(`parameters_values` AS CHAR),'{}') AS `parameters_values`,"
            + "COALESCE(`active`,0) AS `active`"
            + " FROM `shop_payment_systems`" + where + " ORDER BY `id` LIMIT 200");

        var rows = new List<ErpPaymentSystemRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpPaymentSystemRow(
                Convert.ToInt64(reader.GetValue(0)),
                reader.GetValue(1)?.ToString() ?? "",
                reader.GetValue(2)?.ToString() ?? "",
                reader.GetValue(3)?.ToString() ?? "[]",
                reader.GetValue(4)?.ToString() ?? "{}",
                Convert.ToInt32(reader.GetValue(5))));
        }
        return rows;
    }

    public async Task<bool> OfficesProvisionedAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return false;
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await TableExistsAsync(connection, "shop_offices", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ErpPayOfficeRow>> ListOfficesAsync(long adminUserId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "shop_offices", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var hasUsers = await ColumnExistsAsync(connection, "shop_offices", "users", cancellationToken).ConfigureAwait(false);
        var sql = ErpDb.Positional(
            "SELECT `id`,"
            + "COALESCE(CAST(`caption` AS CHAR),'') AS `caption`,"
            + "COALESCE(CAST(`city` AS CHAR),'') AS `city`,"
            + "COALESCE(CAST(`address` AS CHAR),'') AS `address`,"
            + "COALESCE(`pay_system_id`,0) AS `pay_system_id`,"
            + "COALESCE(CAST(`pay_system_parameters` AS CHAR),'{}') AS `pay_system_parameters`"
            + (hasUsers ? ",CAST(`users` AS CHAR) AS `users`" : ",NULL AS `users`")
            + " FROM `shop_offices` ORDER BY `id` LIMIT 500");

        var rows = new List<ErpPayOfficeRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var usersJson = reader.GetValue(6)?.ToString();
            if (hasUsers && adminUserId > 0 && !UserInOffice(usersJson, adminUserId))
            {
                continue;
            }
            rows.Add(new ErpPayOfficeRow(
                Convert.ToInt64(reader.GetValue(0)),
                reader.GetValue(1)?.ToString() ?? "",
                reader.GetValue(2)?.ToString() ?? "",
                reader.GetValue(3)?.ToString() ?? "",
                Convert.ToInt64(reader.GetValue(4)),
                reader.GetValue(5)?.ToString() ?? "{}"));
        }
        return rows;
    }

    public async Task<ErpSimpleWriteResult> SaveStandartAsync(long systemId, string? parametersValues, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAndEnsureSystemsAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return ErpSimpleWriteResult.Fail("not-configured", "Tenant connection is not configured");
        }

        var result = true;
        // PHP: deactivate all systems first (cumulative result flag).
        var deactivated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_payment_systems` SET `active`=0"),
            cancellationToken).ConfigureAwait(false);
        if (deactivated < 0)
        {
            result = false;
        }

        if (systemId > 0)
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_payment_systems` SET `active`=1, `parameters_values` = ? WHERE `id` = ?"),
                cancellationToken,
                parametersValues ?? "{}",
                systemId).ConfigureAwait(false);
            if (updated <= 0)
            {
                result = false;
            }
        }

        return result
            ? ErpSimpleWriteResult.Ok(systemId > 0 ? "Payment system activated" : "All payment systems disabled", systemId)
            : ErpSimpleWriteResult.Fail("save-failed", "Payment system save failed");
    }

    public async Task<ErpSimpleWriteResult> SaveWholesalerAsync(long adminUserId, long officeId, long systemId, string? parametersValues, CancellationToken cancellationToken = default)
    {
        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Office is required");
        }

        await using var connection = await OpenAndEnsureSystemsAsync(cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return ErpSimpleWriteResult.Fail("not-configured", "Tenant connection is not configured");
        }
        if (!await TableExistsAsync(connection, "shop_offices", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("not-provisioned", "Offices table is not provisioned");
        }

        var hasUsers = await ColumnExistsAsync(connection, "shop_offices", "users", cancellationToken).ConfigureAwait(false);
        var officeSql = ErpDb.Positional(
            "SELECT `id`" + (hasUsers ? ",CAST(`users` AS CHAR)" : "") + " FROM `shop_offices` WHERE `id` = ? LIMIT 1");
        string? usersJson = null;
        var officeFound = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = officeSql;
            var param = command.CreateParameter();
            param.ParameterName = "@p0";
            param.Value = officeId;
            command.Parameters.Add(param);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                officeFound = true;
                if (hasUsers)
                {
                    usersJson = reader.GetValue(1)?.ToString();
                }
            }
        }
        if (!officeFound)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Office does not exist");
        }
        if (hasUsers && adminUserId > 0 && !UserInOffice(usersJson, adminUserId))
        {
            return ErpSimpleWriteResult.Fail("forbidden", "You do not manage this office");
        }

        if (systemId != 0)
        {
            var n = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_payment_systems` WHERE `id` = ?"),
                cancellationToken,
                systemId).ConfigureAwait(false);
            if (n == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Payment system does not exist");
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_offices` SET `pay_system_id`=?, `pay_system_parameters`=? WHERE `id`=?"),
            cancellationToken,
            systemId,
            parametersValues ?? "{}",
            officeId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Office payment system saved", officeId);
    }

    private static bool UserInOffice(string? usersJson, long adminUserId)
    {
        if (string.IsNullOrWhiteSpace(usersJson))
        {
            return false;
        }
        try
        {
            using var doc = JsonDocument.Parse(usersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.GetInt64() == adminUserId)
                {
                    return true;
                }
                if (item.ValueKind == JsonValueKind.String && long.TryParse(item.GetString(), out var v) && v == adminUserId)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }
        return false;
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
