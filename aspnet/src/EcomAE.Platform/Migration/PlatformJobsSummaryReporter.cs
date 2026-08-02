using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Read-only platform jobs queue summary for migration diagnostics.
/// Mirrors the read side of PHP <c>epc_platform_jobs</c>. Performs zero writes / claims.
/// </summary>
public sealed class PlatformJobsSummaryReporter : IPlatformJobsSummaryReporter
{
    private readonly ITenantDbConnectionFactory _connections;

    public PlatformJobsSummaryReporter(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<PlatformJobsSummary> BuildAsync(int recentLimit, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(recentLimit, 1, 200);
        if (!_connections.IsConfigured)
        {
            return Empty("migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var byStatus = await StatusBucketsAsync(connection, cancellationToken).ConfigureAwait(false);
            var byType = await TypeBucketsAsync(connection, cancellationToken).ConfigureAwait(false);
            var recent = await RecentAsync(connection, limit, cancellationToken).ConfigureAwait(false);

            var queued = CountStatus(byStatus, "queued");
            var running = CountStatus(byStatus, "running");
            var done = CountStatus(byStatus, "done");
            var failed = CountStatus(byStatus, "failed");
            var total = byStatus.Sum(item => item.Count);

            return new PlatformJobsSummary(
                total,
                queued,
                running,
                done,
                failed,
                byStatus,
                byType,
                recent,
                "database",
                string.Empty);
        }
        catch (Exception ex)
        {
            return Empty("database-error", ex.Message);
        }
    }

    private static PlatformJobsSummary Empty(string source, string message)
        => new(0, 0, 0, 0, 0, [], [], [], source, message);

    private static int CountStatus(IReadOnlyList<PlatformJobsStatusBucket> buckets, string status)
        => buckets.FirstOrDefault(item => string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

    private static async Task<IReadOnlyList<PlatformJobsStatusBucket>> StatusBucketsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyPlatformJobsSql.CountByStatus;
        var rows = new List<PlatformJobsStatusBucket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PlatformJobsStatusBucket(
                Convert.ToString(reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["count"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PlatformJobsTypeBucket>> TypeBucketsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyPlatformJobsSql.CountByType;
        var rows = new List<PlatformJobsTypeBucket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PlatformJobsTypeBucket(
                Convert.ToString(reader["job_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["count"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PlatformJobsRecentRow>> RecentAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyPlatformJobsSql.Recent;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@limit";
        parameter.Value = limit;
        command.Parameters.Add(parameter);

        var rows = new List<PlatformJobsRecentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PlatformJobsRecentRow(
                Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["job_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["tenant_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["priority"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["attempts"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["max_attempts"], CultureInfo.InvariantCulture),
                FormatDbDate(reader["available_at"]),
                FormatDbDate(reader["started_at"]),
                FormatDbDate(reader["finished_at"]),
                Convert.ToString(reader["last_error"], CultureInfo.InvariantCulture) ?? string.Empty,
                FormatDbDate(reader["created_at"]),
                FormatDbDate(reader["updated_at"])));
        }

        return rows;
    }

    private static string? FormatDbDate(object value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        if (value is DateTime dt)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
