using System.Data.Common;
using System.Text.Json;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public interface IErpPermissionScopeReadService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ErpPermissionGrant> Grants, IReadOnlyList<ErpPermissionDelegation> Delegations)> ListForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPermissionScopeReadService : IErpPermissionScopeReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPermissionScopeReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS `epc_erp_permission_grant` (
                `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                `user_id` INT UNSIGNED NOT NULL,
                `group_key` VARCHAR(64) NOT NULL,
                `actions_json` TEXT NULL,
                `company_ids_json` TEXT NULL,
                `site_ids_json` TEXT NULL,
                `approval_limit` DECIMAL(18,2) NULL,
                `effective_from` BIGINT NULL,
                `effective_to` BIGINT NULL,
                PRIMARY KEY (`id`),
                KEY `idx_user_group` (`user_id`,`group_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS `epc_erp_permission_delegation` (
                `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                `principal_user_id` INT UNSIGNED NOT NULL,
                `delegate_user_id` INT UNSIGNED NOT NULL,
                `group_key` VARCHAR(64) NOT NULL,
                `actions_json` TEXT NULL,
                `company_ids_json` TEXT NULL,
                `site_ids_json` TEXT NULL,
                `approval_limit` DECIMAL(18,2) NULL,
                `effective_from` BIGINT NULL,
                `effective_to` BIGINT NULL,
                PRIMARY KEY (`id`),
                KEY `idx_delegate_group` (`delegate_user_id`,`group_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ErpPermissionGrant> Grants, IReadOnlyList<ErpPermissionDelegation> Delegations)> ListForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || userId <= 0)
        {
            return ([], []);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var grants = await ReadGrantsAsync(connection, userId, cancellationToken).ConfigureAwait(false);
            var delegations = await ReadDelegationsAsync(connection, userId, cancellationToken).ConfigureAwait(false);
            return (grants, delegations);
        }
        catch (DbException)
        {
            return ([], []);
        }
    }

    private static async Task<IReadOnlyList<ErpPermissionGrant>> ReadGrantsAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `user_id`,`group_key`,`actions_json`,`company_ids_json`,`site_ids_json`,`approval_limit`,`effective_from`,`effective_to` "
            + "FROM `epc_erp_permission_grant` WHERE `user_id`=?";
        AddParameter(command, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ErpPermissionGrant>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new ErpPermissionGrant(
                Convert.ToInt32(reader["user_id"]),
                Convert.ToString(reader["group_key"]) ?? string.Empty,
                ReadStrings(reader["actions_json"]),
                ReadLongs(reader["company_ids_json"]),
                ReadLongs(reader["site_ids_json"]),
                ReadDecimal(reader["approval_limit"]),
                ReadTime(reader["effective_from"]),
                ReadTime(reader["effective_to"])));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ErpPermissionDelegation>> ReadDelegationsAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `principal_user_id`,`delegate_user_id`,`group_key`,`actions_json`,`company_ids_json`,`site_ids_json`,`approval_limit`,`effective_from`,`effective_to` "
            + "FROM `epc_erp_permission_delegation` WHERE `delegate_user_id`=?";
        AddParameter(command, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ErpPermissionDelegation>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new ErpPermissionDelegation(
                Convert.ToInt32(reader["principal_user_id"]),
                Convert.ToInt32(reader["delegate_user_id"]),
                Convert.ToString(reader["group_key"]) ?? string.Empty,
                ReadStrings(reader["actions_json"]),
                ReadLongs(reader["company_ids_json"]),
                ReadLongs(reader["site_ids_json"]),
                ReadDecimal(reader["approval_limit"]),
                ReadTime(reader["effective_from"]),
                ReadTime(reader["effective_to"])));
        }

        return result;
    }

    private static void AddParameter(DbCommand command, object value)
    {
        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static IReadOnlySet<string>? ReadStrings(object value)
    {
        if (value is DBNull || string.IsNullOrWhiteSpace(Convert.ToString(value)))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HashSet<string>>(Convert.ToString(value)!)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlySet<long>? ReadLongs(object value)
    {
        if (value is DBNull || string.IsNullOrWhiteSpace(Convert.ToString(value)))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HashSet<long>>(Convert.ToString(value)!) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static decimal? ReadDecimal(object value)
        => value is DBNull ? null : Convert.ToDecimal(value);

    private static DateTimeOffset? ReadTime(object value)
        => value is DBNull ? null : DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(value));
}
