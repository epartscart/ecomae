using System.Data.Common;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Persists API usage rows to <c>epc_umapi_usage_log</c> when MySQL is configured.
/// Falls back to in-memory capture when the platform connection string is empty.
/// </summary>
public sealed class DbLegacyApiUsageLogger : ILegacyApiUsageLogger
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly MigrationLegacyApiUsageLogger _fallback = new();
    private readonly TimeProvider _timeProvider;

    public DbLegacyApiUsageLogger(ITenantDbConnectionFactory connections, TimeProvider timeProvider)
    {
        _connections = connections;
        _timeProvider = timeProvider;
    }

    public List<LegacyApiUsageLogEntry> FallbackEntries => _fallback.Entries;

    public async Task LogAsync(LegacyApiUsageLogEntry entry, CancellationToken cancellationToken = default)
    {
        var normalized = entry with
        {
            Action = Truncate(entry.Action, 40),
            Section = Truncate(entry.Section, 20),
            Source = Truncate(string.IsNullOrWhiteSpace(entry.Source) ? "api_client" : entry.Source, 40),
            RequestPath = Truncate(entry.RequestPath, 255),
            Message = Truncate(entry.Message, 255),
            IpAddress = Truncate(entry.IpAddress, 45)
        };

        if (!_connections.IsConfigured)
        {
            await _fallback.LogAsync(normalized, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(databaseName: null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacyApiUsageLogSql.InsertUsage;
            AddParameter(command, "@createdAt", _timeProvider.GetUtcNow().ToUnixTimeSeconds());
            AddParameter(command, "@action", normalized.Action);
            AddParameter(command, "@section", normalized.Section);
            AddParameter(command, "@source", normalized.Source);
            AddParameter(command, "@clientId", normalized.ClientId is null ? DBNull.Value : normalized.ClientId.Value);
            AddParameter(command, "@requestPath", normalized.RequestPath);
            AddParameter(command, "@httpStatus", normalized.HttpStatus);
            AddParameter(command, "@quotaBlocked", normalized.QuotaBlocked ? 1 : 0);
            AddParameter(command, "@message", string.IsNullOrWhiteSpace(normalized.Message) ? DBNull.Value : normalized.Message);
            AddParameter(command, "@ip", normalized.IpAddress);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Match PHP epc_api_clients_log_usage(): never fail the request on log write errors.
            await _fallback.LogAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
