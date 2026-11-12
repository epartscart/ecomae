using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Tenant-wise catalog-provider usage desk. Reads <c>epc_umapi_usage_log</c> — every
/// storefront/API request already writes a row (action, source, http_status, from_cache,
/// quota_blocked, is_live) — and reports daily totals so operators can see where quota
/// goes and how much of the VIN reserve is left.
/// </summary>
public interface ICpCatalogUsageDeskService
{
    Task<CpCatalogUsageDeskView> LoadAsync(int days, CancellationToken cancellationToken = default);
}

public sealed record CpCatalogUsageActionRow(
    string Action,
    long Calls,
    long LiveCalls,
    long CachedHits,
    long QuotaBlocked);

public sealed record CpCatalogUsageDayRow(
    string UsageDate,
    long Calls,
    long LiveCalls,
    long CachedHits,
    long QuotaBlocked,
    long VinCalls);

public sealed record CpCatalogUsageSourceRow(string Source, long Calls, long QuotaBlocked);

public sealed record CpCatalogUsageRecentRow(
    string CreatedAt,
    string Action,
    string Source,
    int HttpStatus,
    bool FromCache,
    bool QuotaBlocked,
    string Message);

public sealed record CpCatalogUsageDeskView(
    int Days,
    long TodayCalls,
    long TodayLive,
    long TodayCached,
    long TodayQuotaBlocked,
    long TodayVinCalls,
    long VinReserveLimit,
    IReadOnlyList<CpCatalogUsageActionRow> TodayByAction,
    IReadOnlyList<CpCatalogUsageSourceRow> TodayBySource,
    IReadOnlyList<CpCatalogUsageDayRow> Daily,
    IReadOnlyList<CpCatalogUsageRecentRow> Recent,
    string Source,
    string Error)
{
    public static CpCatalogUsageDeskView Empty(int days, string source, string error) =>
        new(days, 0, 0, 0, 0, 0, VinReserve, [], [], [], [], source, error);

    /// <summary>VIN calls guaranteed capacity — daily provider quota minus the VIN reserve.</summary>
    public const long DailyQuota = 10_000;
    public const long VinReserve = 1_000;

    public long VinReserveLeft => Math.Max(0, VinReserve - TodayVinCalls);

    /// <summary>Only live upstream calls consume provider quota — cache hits and
    /// quota-blocked attempts never reach the provider.</summary>
    public long TodayPercent => Math.Min(100, TodayLive * 100 / DailyQuota);
}

public sealed class CpCatalogUsageDeskService : ICpCatalogUsageDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCatalogUsageDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpCatalogUsageDeskView> LoadAsync(int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 60);
        if (!_connections.IsConfigured)
        {
            return CpCatalogUsageDeskView.Empty(days, "unconfigured", "Tenant database unavailable.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "epc_umapi_usage_log", cancellationToken).ConfigureAwait(false))
            {
                return CpCatalogUsageDeskView.Empty(days, "no-table", "Usage log table not present on this tenant yet.");
            }

            var today = await TodayTotalsAsync(connection, cancellationToken).ConfigureAwait(false);
            var byAction = await TodayByActionAsync(connection, cancellationToken).ConfigureAwait(false);
            var bySource = await TodayBySourceAsync(connection, cancellationToken).ConfigureAwait(false);
            var daily = await DailyAsync(connection, days, cancellationToken).ConfigureAwait(false);
            var recent = await RecentAsync(connection, cancellationToken).ConfigureAwait(false);
            var vin = await VinTodayAsync(connection, cancellationToken).ConfigureAwait(false);

            return new CpCatalogUsageDeskView(
                days,
                today.Calls,
                today.Live,
                today.Cached,
                today.Blocked,
                vin,
                CpCatalogUsageDeskView.VinReserve,
                byAction,
                bySource,
                daily,
                recent,
                "database",
                string.Empty);
        }
        catch (DbException)
        {
            return CpCatalogUsageDeskView.Empty(days, "database-error", "Tenant database unavailable.");
        }
    }

    private sealed record Totals(long Calls, long Live, long Cached, long Blocked);

    private static async Task<bool> TableExistsAsync(
        DbConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables "
            + "WHERE table_schema = DATABASE() AND table_name = @t";
        Add(command, "@t", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    private static async Task<Totals> TodayTotalsAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*), COALESCE(SUM(is_live),0), COALESCE(SUM(from_cache),0), COALESCE(SUM(quota_blocked),0) "
            + "FROM `epc_umapi_usage_log` WHERE `usage_date` = CURDATE()";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new Totals(Int64(reader, 0), Int64(reader, 1), Int64(reader, 2), Int64(reader, 3))
            : new Totals(0, 0, 0, 0);
    }

    private static async Task<long> VinTodayAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM `epc_umapi_usage_log` WHERE `usage_date` = CURDATE() AND `action` LIKE 'vin%' "
            + "AND `is_live` = 1 AND `from_cache` = 0 AND `quota_blocked` = 0";
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    private static async Task<IReadOnlyList<CpCatalogUsageActionRow>> TodayByActionAsync(
        DbConnection connection, CancellationToken ct)
    {
        var rows = new List<CpCatalogUsageActionRow>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `action`, COUNT(*), COALESCE(SUM(is_live),0), COALESCE(SUM(from_cache),0), "
            + "COALESCE(SUM(quota_blocked),0) FROM `epc_umapi_usage_log` "
            + "WHERE `usage_date` = CURDATE() GROUP BY `action` ORDER BY COUNT(*) DESC";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new CpCatalogUsageActionRow(
                reader.GetString(0), Int64(reader, 1), Int64(reader, 2),
                Int64(reader, 3), Int64(reader, 4)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpCatalogUsageSourceRow>> TodayBySourceAsync(
        DbConnection connection, CancellationToken ct)
    {
        var rows = new List<CpCatalogUsageSourceRow>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `source`, COUNT(*), COALESCE(SUM(quota_blocked),0) FROM `epc_umapi_usage_log` "
            + "WHERE `usage_date` = CURDATE() GROUP BY `source` ORDER BY COUNT(*) DESC";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new CpCatalogUsageSourceRow(reader.GetString(0), Int64(reader, 1), Int64(reader, 2)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpCatalogUsageDayRow>> DailyAsync(
        DbConnection connection, int days, CancellationToken ct)
    {
        var rows = new List<CpCatalogUsageDayRow>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `usage_date`, COUNT(*), COALESCE(SUM(is_live),0), COALESCE(SUM(from_cache),0), "
            + "COALESCE(SUM(quota_blocked),0), COALESCE(SUM(`action` LIKE 'vin%' AND is_live = 1 AND from_cache = 0 AND quota_blocked = 0),0) "
            + "FROM `epc_umapi_usage_log` WHERE `usage_date` >= CURDATE() - INTERVAL @days DAY "
            + "GROUP BY `usage_date` ORDER BY `usage_date` DESC";
        Add(command, "@days", days - 1);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new CpCatalogUsageDayRow(
                Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                Int64(reader, 1), Int64(reader, 2), Int64(reader, 3),
                Int64(reader, 4), Int64(reader, 5)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpCatalogUsageRecentRow>> RecentAsync(
        DbConnection connection, CancellationToken ct)
    {
        var rows = new List<CpCatalogUsageRecentRow>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `created_at`, `action`, `source`, `http_status`, `from_cache`, `quota_blocked`, "
            + "COALESCE(`message`, '') FROM `epc_umapi_usage_log` ORDER BY `id` DESC LIMIT 60";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var stamp = DateTimeOffset
                .FromUnixTimeSeconds(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture))
                .UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
            rows.Add(new CpCatalogUsageRecentRow(
                stamp,
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0,
                Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return rows;
    }

    private static long Int64(DbDataReader reader, int ordinal)
        => Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
