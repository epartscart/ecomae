using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Read-only session/identity lookup against PHP sessions/users/groups/modules. Performs zero writes.
/// </summary>
public sealed class DbLegacySessionStore : ILegacySessionStore
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbLegacySessionStore(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public bool IsConfigured => _connections.IsConfigured;

    public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        => ExistsAsync(LegacySessionSql.CountAdminSession, sessionToken, userId, cancellationToken);

    public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        => ExistsAsync(LegacySessionSql.CountCustomerSession, sessionToken, userId, cancellationToken);

    public async Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || userId <= 0)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        var email = await ScalarStringAsync(connection, LegacySessionSql.SelectUserEmail, userId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var userGroups = await IntListAsync(connection, LegacySessionSql.SelectUserGroupIds, "@userId", userId, cancellationToken).ConfigureAwait(false);
        var backendGroups = await IntListAsync(connection, LegacySessionSql.SelectBackendGroupIds, null, null, cancellationToken).ConfigureAwait(false);
        if (backendGroups.Count == 0)
        {
            // PHP epc_auth_backend_group_ids falls back to group id 3 when none marked for_backend.
            backendGroups = [3];
        }

        var backendSet = backendGroups.ToHashSet();
        var hasBackend = userGroups.Any(backendSet.Contains);
        var modules = await LoadModuleAclAsync(connection, userGroups, cancellationToken).ConfigureAwait(false);
        return new LegacyAdminIdentity(email, userGroups, hasBackend, modules);
    }

    private static async Task<IReadOnlyList<ModuleAclEntry>> LoadModuleAclAsync(
        DbConnection connection,
        IReadOnlyList<int> groupIds,
        CancellationToken cancellationToken)
    {
        var byId = new Dictionary<int, ModuleAclEntry>();

        try
        {
            await using (var openCommand = connection.CreateCommand())
            {
                openCommand.CommandText = LegacySessionSql.SelectOpenModules;
                await using var openReader = await openCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await openReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt32(openReader["id"], CultureInfo.InvariantCulture);
                    var caption = Convert.ToString(openReader["caption"], CultureInfo.InvariantCulture) ?? string.Empty;
                    byId[id] = new ModuleAclEntry(id, caption, OpenAccess: true);
                }
            }

            foreach (var groupId in groupIds.Distinct())
            {
                await using var grantCommand = connection.CreateCommand();
                grantCommand.CommandText = LegacySessionSql.SelectModuleAccessForGroup;
                AddParameter(grantCommand, "@groupId", groupId);
                await using var grantReader = await grantCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await grantReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt32(grantReader["module_id"], CultureInfo.InvariantCulture);
                    var caption = Convert.ToString(grantReader["caption"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!byId.ContainsKey(id))
                    {
                        byId[id] = new ModuleAclEntry(id, caption, OpenAccess: false);
                    }
                }
            }
        }
        catch
        {
            // Missing modules tables degrade to empty ACL for migration safety.
            return Array.Empty<ModuleAclEntry>();
        }

        return byId.Values.OrderBy(item => item.ModuleId).ToArray();
    }

    private async Task<bool> ExistsAsync(string sql, string sessionToken, int userId, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured
            || string.IsNullOrWhiteSpace(sessionToken)
            || userId <= 0)
        {
            return false;
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@session", sessionToken);
        AddParameter(command, "@userId", userId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(scalar ?? 0, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<string?> ScalarStringAsync(DbConnection connection, string sql, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@userId", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static async Task<List<int>> IntListAsync(
        DbConnection connection,
        string sql,
        string? parameterName,
        object? parameterValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameterName is not null && parameterValue is not null)
        {
            AddParameter(command, parameterName, parameterValue);
        }

        var rows = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return rows;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
