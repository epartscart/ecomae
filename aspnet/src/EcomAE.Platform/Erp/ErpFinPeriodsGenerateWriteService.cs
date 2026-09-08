using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fin_periods_generate</c> / ajax <c>fin_periods_generate</c> twin.
/// INSERT/refresh 12 rows on <c>epc_fin_periods</c> (keeps existing status).
/// Company comes from <c>?company=</c> / first active legal entity, not POST.
/// Period status, FX/alloc/accrual, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpFinPeriodsGenerateWriteService
{
    Task<ErpSimpleWriteResult> GenerateAsync(
        ErpFinPeriodsGenerateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFinPeriodsGenerateWriteRequest(
    int Fy = 0,
    int StartMonth = 1,
    long CompanyHint = 0);

public sealed class ErpFinPeriodsGenerateWriteService : IErpFinPeriodsGenerateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinPeriodsGenerateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> GenerateAsync(
        ErpFinPeriodsGenerateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var periods = PeriodDates(request.Fy, request.StartMonth);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_fin_periods", "start_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Finance periods table is not provisioned");
        }

        var companyId = await ResolveActiveCompanyIdAsync(connection, request.CompanyHint, cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var period in periods)
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `epc_fin_periods` (`company_id`,`fy`,`period_no`,`start_date`,`end_date`,`status`,`time_created`) VALUES (?,?,?,?,?,'open',?) ON DUPLICATE KEY UPDATE `start_date`=VALUES(`start_date`), `end_date`=VALUES(`end_date`)"),
                cancellationToken,
                companyId,
                request.Fy,
                period.PeriodNo,
                period.StartUnix,
                period.EndUnix,
                now).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Generated " + periods.Count.ToString(CultureInfo.InvariantCulture) + " periods", companyId);
    }

    public static IReadOnlyList<(int PeriodNo, long StartUnix, long EndUnix)> PeriodDates(int fy, int startMonth)
    {
        if (startMonth < 1 || startMonth > 12)
        {
            startMonth = 1;
        }

        var year = PhpMktimeYear(fy);
        var list = new List<(int, long, long)>(12);
        for (var p = 0; p < 12; p++)
        {
            var m = startMonth + p;
            var y = year + ((m - 1) / 12);
            var mm = ((m - 1) % 12) + 1;
            var start = new DateTimeOffset(y, mm, 1, 0, 0, 0, TimeSpan.Zero);
            var lastDay = DateTime.DaysInMonth(y, mm);
            var end = new DateTimeOffset(y, mm, lastDay, 23, 59, 59, TimeSpan.Zero);
            list.Add((p + 1, start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds()));
        }

        return list;
    }

    /// <summary>PHP <c>mktime</c> two-digit year: 0-69 → 2000-2069, 70-100 → 1970-2000.</summary>
    public static int PhpMktimeYear(int fy)
    {
        if (fy >= 0 && fy <= 69)
        {
            return 2000 + fy;
        }

        if (fy >= 70 && fy <= 100)
        {
            return 1900 + fy;
        }

        return fy;
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
    }

    public static int JsonInt(JsonElement root, params string[] names) =>
        (int)JsonLong(root, names);

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static bool JsonHas(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<long> ResolveActiveCompanyIdAsync(
        DbConnection connection,
        long hint,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "epc_erp_pm_legal_entities", "id", cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var ids = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        if (hint > 0 && ids.Contains(hint))
        {
            return hint;
        }

        return ids[0];
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
