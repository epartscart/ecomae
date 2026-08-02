using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Platform-DB backed API client store (epc_api_clients). Uses the registry/platform
/// connection without tenant database overrides, matching PHP epc_api_clients_platform_pdo().
/// </summary>
public sealed class DbLegacyApiClientStore : ILegacyApiClientStore
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly TimeProvider _timeProvider;

    public DbLegacyApiClientStore(ITenantDbConnectionFactory connections, TimeProvider timeProvider)
    {
        _connections = connections;
        _timeProvider = timeProvider;
    }

    public bool IsConfigured => _connections.IsConfigured;

    public async Task<LegacyApiClientRecord?> FindActiveByHashAsync(string sha256Hash, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(sha256Hash))
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(databaseName: null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyApiClientSql.FetchActiveClientByHash;
        AddParameter(command, "@hash", sha256Hash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return MapClient(reader);
    }

    public async Task ResetDailyQuotaIfNeededAsync(LegacyApiClientRecord client, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || client.CallsResetDate == today)
        {
            return;
        }

        await using var connection = await _connections.OpenAsync(databaseName: null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyApiClientSql.ResetDailyQuota;
        AddParameter(command, "@today", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddParameter(command, "@now", UnixNow());
        AddParameter(command, "@id", client.Id);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryConsumeDailyQuotaAsync(LegacyApiClientRecord client, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var limit = Math.Max(1, client.DailyLimit);
        await using var connection = await _connections.OpenAsync(databaseName: null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyApiClientSql.ConsumeDailyQuota;
        AddParameter(command, "@now", UnixNow());
        AddParameter(command, "@id", client.Id);
        AddParameter(command, "@dailyLimit", limit);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private long UnixNow() => _timeProvider.GetUtcNow().ToUnixTimeSeconds();

    private static LegacyApiClientRecord MapClient(DbDataReader reader)
    {
        return new LegacyApiClientRecord(
            Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["client_key_hash"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["client_key_prefix"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["product"], CultureInfo.InvariantCulture) ?? "catalog",
            Convert.ToString(reader["label"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt32(reader["active"], CultureInfo.InvariantCulture) == 1,
            Convert.ToInt32(reader["daily_limit"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["calls_today"], CultureInfo.InvariantCulture),
            ParseDateOnly(reader["calls_reset_date"]),
            Convert.ToString(reader["allowed_actions_json"], CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static DateOnly? ParseDateOnly(object value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        if (DateOnly.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
