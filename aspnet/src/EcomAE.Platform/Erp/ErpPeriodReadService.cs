using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_period_list</c> / <c>epc_erp_period_checklist</c> / <c>epc_erp_period_close_summary</c>
/// twins (ajax <c>period_list</c>, <c>period_checklist</c>, <c>period_summary</c>). List ensures the current and
/// two previous months exist (PHP INSERT … ON DUPLICATE KEY); summary auto-creates the current period via
/// <c>epc_erp_period_get</c>. Does not CREATE tables.
/// </summary>
public interface IErpPeriodReadService
{
    Task<ErpPeriodListResult> ListAsync(int limit = 24, CancellationToken cancellationToken = default);

    Task<ErpPeriodChecklistResult> ChecklistAsync(string? yearMonth, CancellationToken cancellationToken = default);

    Task<ErpPeriodSummaryResult> SummaryAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpPeriodRow(
    long Id,
    string YearMonth,
    int Year,
    int Month,
    string Status,
    long ClosedBy,
    long ClosedAt,
    long LockedBy,
    long LockedAt,
    string Note,
    JsonElement Checklist,
    long CreatedAt,
    long UpdatedAt);

public sealed record ErpPeriodListResult(ErpSimpleWriteResult Result, IReadOnlyList<ErpPeriodRow> Periods);

public sealed record ErpPeriodChecklistResult(ErpSimpleWriteResult Result, string YearMonth, IReadOnlyList<ErpPeriodChecklistItem> Checklist);

public sealed record ErpPeriodSummaryResult(
    ErpSimpleWriteResult Result,
    string CurrentPeriod,
    string CurrentStatus,
    long FiscalLockDate,
    int OpenPeriods,
    int SoftClosePeriods,
    int LockedPeriods,
    int ChecklistBlockers,
    int ChecklistWarnings,
    IReadOnlyList<ErpPeriodChecklistItem> Checklist);

public sealed class ErpPeriodReadService : IErpPeriodReadService
{
    public const string NotProvisioned = "Period close tables are not provisioned";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpPeriodReadService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    public static string CurrentYearMonth(DateTimeOffset now) => now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>PHP: <c>date('Y-m', strtotime("-$i months"))</c> for i = 0..2.</summary>
    public static IReadOnlyList<string> RecentYearMonths(DateTimeOffset now)
        => [CurrentYearMonth(now), CurrentYearMonth(now.AddMonths(-1)), CurrentYearMonth(now.AddMonths(-2))];

    public static (int Blockers, int Warnings) CountSeverities(IReadOnlyList<ErpPeriodChecklistItem> checklist)
    {
        var blockers = 0;
        var warnings = 0;
        foreach (var item in checklist)
        {
            if (item.Count <= 0)
            {
                continue;
            }

            if (item.Severity == "blocker")
            {
                blockers++;
            }
            else if (item.Severity == "warning")
            {
                warnings++;
            }
        }

        return (blockers, warnings);
    }

    public async Task<ErpPeriodListResult> ListAsync(int limit = 24, CancellationToken cancellationToken = default)
    {
        var empty = Array.Empty<ErpPeriodRow>();
        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), empty);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpPeriodLockReopenWriteService.SchemaReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new(ErpSimpleWriteResult.Fail("invalid", NotProvisioned), empty);
        }

        var now = _clock.GetUtcNow();
        var unix = now.ToUnixTimeSeconds();
        foreach (var ym in RecentYearMonths(now))
        {
            await ErpPeriodLockReopenWriteService.EnsurePeriodAsync(connection, null, ym, unix, cancellationToken).ConfigureAwait(false);
        }

        var rows = new List<ErpPeriodRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`,`year_month`,`year`,`month`,`status`,`closed_by`,`closed_at`,`locked_by`,`locked_at`,`note`,`checklist_json`,`created_at`,`updated_at` FROM `epc_erp_periods` ORDER BY `year_month` DESC LIMIT " + Math.Max(1, limit).ToString(CultureInfo.InvariantCulture);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? "open",
                    Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(9), CultureInfo.InvariantCulture) ?? "",
                    DecodeChecklist(reader.IsDBNull(10) ? null : reader.GetString(10)),
                    Convert.ToInt64(reader.GetValue(11), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(12), CultureInfo.InvariantCulture)));
            }
        }

        return new(ErpSimpleWriteResult.Ok("OK", 0) with { Writes = 0 }, rows);
    }

    public async Task<ErpPeriodChecklistResult> ChecklistAsync(string? yearMonth, CancellationToken cancellationToken = default)
    {
        var ym = string.IsNullOrEmpty(yearMonth) ? CurrentYearMonth(_clock.GetUtcNow()) : yearMonth;
        var empty = Array.Empty<ErpPeriodChecklistItem>();
        if (!ErpPeriodLockReopenWriteService.IsCalendarYearMonth(ym))
        {
            return new(ErpSimpleWriteResult.Fail("invalid", "Invalid year_month format: " + ym), ym, empty);
        }

        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), ym, empty);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpPeriodLockReopenWriteService.SchemaReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new(ErpSimpleWriteResult.Fail("invalid", NotProvisioned), ym, empty);
        }

        var checklist = await ErpPeriodLockReopenWriteService.ChecklistAsync(connection, null, ym, cancellationToken).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok("OK", 0) with { Writes = 0 }, ym, checklist);
    }

    public async Task<ErpPeriodSummaryResult> SummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var current = CurrentYearMonth(now);
        var empty = Array.Empty<ErpPeriodChecklistItem>();
        if (!_connections.IsConfigured)
        {
            return Fail(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), current, empty);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpPeriodLockReopenWriteService.SchemaReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return Fail(ErpSimpleWriteResult.Fail("invalid", NotProvisioned), current, empty);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `status`, COUNT(*) FROM `epc_erp_periods` GROUP BY `status`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                counts[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? ""] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }

        var status = await ErpPeriodLockReopenWriteService.EnsurePeriodAsync(connection, null, current, now.ToUnixTimeSeconds(), cancellationToken).ConfigureAwait(false);
        var checklist = await ErpPeriodLockReopenWriteService.ChecklistAsync(connection, null, current, cancellationToken).ConfigureAwait(false);
        long lockDate;
        try
        {
            lockDate = await ErpDb.LongAsync(connection, null, "SELECT MAX(`lock_date`) FROM `epc_erp_fiscal_locks` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            lockDate = 0;
        }

        var (blockers, warnings) = CountSeverities(checklist);
        return new(
            ErpSimpleWriteResult.Ok("OK", 0) with { Writes = 0 },
            current,
            status,
            lockDate,
            counts.GetValueOrDefault("open"),
            counts.GetValueOrDefault("soft_close"),
            counts.GetValueOrDefault("locked"),
            blockers,
            warnings,
            checklist);
    }

    private static ErpPeriodSummaryResult Fail(ErpSimpleWriteResult result, string current, IReadOnlyList<ErpPeriodChecklistItem> empty)
        => new(result, current, "", 0, 0, 0, 0, 0, 0, empty);

    /// <summary>PHP: <c>json_decode($r['checklist_json'] ?? '[]', true) ?: array()</c>.</summary>
    public static JsonElement DecodeChecklist(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    return doc.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
            }
        }

        using var empty = JsonDocument.Parse("[]");
        return empty.RootElement.Clone();
    }
}
