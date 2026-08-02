using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;
using Microsoft.Extensions.Configuration;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Read-only UMAPI usage summary for migration diagnostics (not an external catalog API).
/// Mirrors PHP <c>epc_umapi_usage_summary</c>. Performs zero writes.
/// </summary>
public sealed class UmapiUsageSummaryReporter : IUmapiUsageSummaryReporter
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly int _dailyLimit;

    public UmapiUsageSummaryReporter(ITenantDbConnectionFactory connections, IConfiguration configuration)
    {
        _connections = connections;
        _dailyLimit = int.TryParse(configuration["Umapi:DailyLimit"], out var limit) && limit > 0
            ? limit
            : 1000;
    }

    public async Task<UmapiUsageSummary> BuildAsync(int days, CancellationToken cancellationToken = default)
    {
        var safeDays = Math.Clamp(days, 1, 30);
        if (!_connections.IsConfigured)
        {
            return Empty("migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var todayLive = await ScalarAsync(connection, LegacyUmapiUsageSql.CountTodayLive, cancellationToken).ConfigureAwait(false);
            var todayCache = await ScalarAsync(connection, LegacyUmapiUsageSql.CountTodayCache, cancellationToken).ConfigureAwait(false);
            var todayBlocked = await ScalarAsync(connection, LegacyUmapiUsageSql.CountTodayBlocked, cancellationToken).ConfigureAwait(false);
            var byAction = await BucketsAsync(connection, LegacyUmapiUsageSql.ByActionToday, "action", cancellationToken).ConfigureAwait(false);
            var bySource = await BucketsAsync(connection, LegacyUmapiUsageSql.BySourceToday, "source", cancellationToken).ConfigureAwait(false);
            var history = await HistoryAsync(connection, safeDays, cancellationToken).ConfigureAwait(false);
            var recent = await RecentAsync(connection, 100, cancellationToken).ConfigureAwait(false);

            var remaining = Math.Max(0, _dailyLimit - todayLive);
            var pct = _dailyLimit > 0 ? Math.Round(todayLive * 100.0 / _dailyLimit, 1) : 0;

            return new UmapiUsageSummary(
                _dailyLimit,
                todayLive,
                todayCache,
                todayBlocked,
                remaining,
                pct,
                todayLive >= _dailyLimit,
                byAction,
                bySource,
                history,
                recent,
                "database",
                string.Empty);
        }
        catch (Exception ex)
        {
            return Empty("database-error", ex.Message);
        }
    }

    private UmapiUsageSummary Empty(string source, string message)
        => new(_dailyLimit, 0, 0, 0, _dailyLimit, 0, false, [], [], [], [], source, message);

    private static async Task<int> ScalarAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<UmapiUsageBucket>> BucketsAsync(
        DbConnection connection,
        string sql,
        string keyColumn,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new List<UmapiUsageBucket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new UmapiUsageBucket(
                Convert.ToString(reader[keyColumn], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["live"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["cache"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["blocked"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<UmapiUsageDay>> HistoryAsync(
        DbConnection connection,
        int days,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyUmapiUsageSql.History;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@daysMinusOne";
        parameter.Value = days - 1;
        command.Parameters.Add(parameter);

        var rows = new List<UmapiUsageDay>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new UmapiUsageDay(
                Convert.ToString(reader["usage_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["live"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["cache"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["blocked"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<UmapiUsageRecentEvent>> RecentAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyUmapiUsageSql.RecentToday;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@limit";
        parameter.Value = Math.Clamp(limit, 1, 500);
        command.Parameters.Add(parameter);

        var rows = new List<UmapiUsageRecentEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var createdAt = Convert.ToInt64(reader["created_at"], CultureInfo.InvariantCulture);
            var time = DateTimeOffset.FromUnixTimeSeconds(createdAt).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            rows.Add(new UmapiUsageRecentEvent(
                createdAt,
                time,
                Convert.ToString(reader["action"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["section"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["source"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["request_path"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["http_status"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["from_cache"], CultureInfo.InvariantCulture) == 1,
                Convert.ToInt32(reader["quota_blocked"], CultureInfo.InvariantCulture) == 1,
                Convert.ToInt32(reader["is_live"], CultureInfo.InvariantCulture) == 1,
                Convert.ToString(reader["message"], CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return rows;
    }
}
