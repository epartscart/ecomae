using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Read-only session lookup against PHP <c>sessions</c>. Performs zero writes.
/// </summary>
public sealed class DbLegacySessionStore : ILegacySessionStore
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbLegacySessionStore(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public bool IsConfigured => _connections.IsConfigured;

    public async Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured
            || string.IsNullOrWhiteSpace(sessionToken)
            || userId <= 0)
        {
            return false;
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySessionSql.CountAdminSession;
        AddParameter(command, "@session", sessionToken);
        AddParameter(command, "@userId", userId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(scalar ?? 0, CultureInfo.InvariantCulture) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
