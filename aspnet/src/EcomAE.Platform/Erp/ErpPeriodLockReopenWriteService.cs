using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_period_lock</c> / <c>epc_erp_period_reopen</c> twins (ajax <c>period_lock</c> / <c>period_reopen</c>).
/// Lock: runs the PHP pre-close checklist, refuses on blockers, moves the fiscal lock to the month end,
/// UPDATEs <c>epc_erp_periods</c> and logs <c>epc_erp_period_close_log</c>. Reopen: reverts the fiscal lock
/// to the previous locked month (or clears it), sets the period open and logs. Does not CREATE tables.
/// </summary>
public interface IErpPeriodLockReopenWriteService
{
    Task<ErpPeriodLockWriteResult> LockAsync(ErpPeriodLockWriteRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ReopenAsync(ErpPeriodLockWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record ErpPeriodLockWriteRequest(string? YearMonth = null, string? Note = null, long AdminId = 0);

public sealed record ErpPeriodChecklistItem(string Id, string Label, long Count, string Severity, string Help);

public sealed record ErpPeriodLockWriteResult(ErpSimpleWriteResult Result, IReadOnlyList<ErpPeriodChecklistItem> Checklist);

public sealed class ErpPeriodLockReopenWriteService : IErpPeriodLockReopenWriteService
{
    public const string AlreadyLocked = "Period is already locked.";
    public const string AlreadyOpen = "Period is already open.";
    public const string Blockers = "Cannot lock period — resolve blockers first.";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpPeriodLockReopenWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpPeriodLockWriteResult> LockAsync(ErpPeriodLockWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var yearMonth = request.YearMonth ?? "";
        var note = request.Note ?? "";
        var empty = Array.Empty<ErpPeriodChecklistItem>();
        if (!IsCalendarYearMonth(yearMonth))
        {
            return new(ErpSimpleWriteResult.Fail("invalid", "Invalid year_month format: " + yearMonth), empty);
        }

        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), empty);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await SchemaReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new(ErpSimpleWriteResult.Fail("invalid", "Period close tables are not provisioned"), empty);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var status = await EnsurePeriodAsync(connection, tx, yearMonth, now, cancellationToken).ConfigureAwait(false);
        if (status == "locked")
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(ErpSimpleWriteResult.Fail("invalid", AlreadyLocked), empty);
        }

        var checklist = await ChecklistAsync(connection, tx, yearMonth, cancellationToken).ConfigureAwait(false);
        if (HasBlockers(checklist))
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(ErpSimpleWriteResult.Fail("invalid", Blockers), checklist);
        }

        var lastDay = MonthEndUnix(yearMonth);
        await SetFiscalLockAsync(connection, tx, lastDay, "Period lock: " + yearMonth, request.AdminId, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "UPDATE `epc_erp_periods` SET `status` = 'locked', `locked_by` = ?, `locked_at` = ?, `note` = ?, `checklist_json` = ?, `updated_at` = ? WHERE `year_month` = ?"),
            cancellationToken,
            request.AdminId,
            now,
            note,
            JsonSerializer.Serialize(checklist.Select(c => new { id = c.Id, label = c.Label, count = c.Count, severity = c.Severity, help = c.Help })),
            now,
            yearMonth).ConfigureAwait(false);
        await CloseLogAsync(connection, tx, yearMonth, "lock", status, "locked", request.AdminId, note, now, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok("Period locked: " + yearMonth, 0), checklist);
    }

    public async Task<ErpSimpleWriteResult> ReopenAsync(ErpPeriodLockWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var yearMonth = request.YearMonth ?? "";
        var note = request.Note ?? "";
        if (!IsCalendarYearMonth(yearMonth))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid year_month format: " + yearMonth);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await SchemaReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Period close tables are not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var status = await EnsurePeriodAsync(connection, tx, yearMonth, now, cancellationToken).ConfigureAwait(false);
        if (status == "open")
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", AlreadyOpen);
        }

        var lastDay = MonthEndUnix(yearMonth);
        var currentLock = await ErpDb.LongAsync(
            connection, tx, "SELECT COALESCE(MAX(`lock_date`), 0) FROM `epc_erp_fiscal_locks` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        if (currentLock > 0 && currentLock <= lastDay)
        {
            var prevLocked = await ErpDb.StringAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `year_month` FROM `epc_erp_periods` WHERE `status` = 'locked' AND `year_month` < ? ORDER BY `year_month` DESC LIMIT 1"),
                cancellationToken,
                yearMonth).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(prevLocked))
            {
                await SetFiscalLockAsync(connection, tx, MonthEndUnix(prevLocked), "Reopened " + yearMonth + "; lock reverted to " + prevLocked, request.AdminId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SetFiscalLockAsync(connection, tx, 0, "Reopened " + yearMonth + "; all periods now open", request.AdminId, cancellationToken).ConfigureAwait(false);
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_erp_periods` SET `status` = 'open', `note` = ?, `updated_at` = ? WHERE `year_month` = ?"),
            cancellationToken,
            note,
            now,
            yearMonth).ConfigureAwait(false);
        await CloseLogAsync(connection, tx, yearMonth, "reopen", status, "open", request.AdminId, note, now, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Period reopened: " + yearMonth, 0);
    }

    /// <summary>PHP <c>YYYY-MM</c> regex plus a real calendar month (PHP mktime would silently roll month 13 over).</summary>
    public static bool IsCalendarYearMonth(string? yearMonth)
        => ErpPeriodSoftCloseWriteService.IsPhpYearMonth(yearMonth)
           && int.Parse(yearMonth![5..], CultureInfo.InvariantCulture) is >= 1 and <= 12;

    /// <summary>PHP: any checklist item with severity <c>blocker</c> and count &gt; 0 refuses the lock.</summary>
    public static bool HasBlockers(IReadOnlyList<ErpPeriodChecklistItem> checklist)
        => checklist.Any(c => c.Severity == "blocker" && c.Count > 0);

    /// <summary>PHP <c>mktime(23,59,59,$m,date('t'),$y)</c> in UTC.</summary>
    public static long MonthEndUnix(string yearMonth)
    {
        var year = int.Parse(yearMonth[..4], CultureInfo.InvariantCulture);
        var month = int.Parse(yearMonth[5..], CultureInfo.InvariantCulture);
        var last = DateTime.DaysInMonth(year, month);
        return new DateTimeOffset(year, month, last, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    public static long MonthStartUnix(string yearMonth)
    {
        var year = int.Parse(yearMonth[..4], CultureInfo.InvariantCulture);
        var month = int.Parse(yearMonth[5..], CultureInfo.InvariantCulture);
        return new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    internal static async Task<IReadOnlyList<ErpPeriodChecklistItem>> ChecklistAsync(
        DbConnection connection, DbTransaction? tx, string yearMonth, CancellationToken ct)
    {
        var start = MonthStartUnix(yearMonth);
        var end = MonthEndUnix(yearMonth);
        var items = new List<ErpPeriodChecklistItem>(7)
        {
            new("draft_so", "Draft Sales Orders",
                await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft' AND `time_created` >= ? AND `time_created` <= ?", ct, start, end).ConfigureAwait(false),
                "warning", "Confirm or cancel draft sales orders before closing the period."),
            new("open_po", "Open Purchase Orders",
                await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft','pending') AND `order_date` >= ? AND `order_date` <= ?", ct, start, end).ConfigureAwait(false),
                "warning", "Receive or cancel open purchase orders before closing."),
            new("unposted_journals", "Unposted GL Journals",
                await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `status` = 'draft' AND `active` = 1 AND `journal_date` >= ? AND `journal_date` <= ?", ct, start, end).ConfigureAwait(false),
                "blocker", "Post or delete draft journals. Unposted journals prevent period lock."),
            new("unreconciled_bank", "Unreconciled Bank Entries",
                await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_cash_bank_entries` WHERE `reconciled` = 0 AND `entry_date` >= ? AND `entry_date` <= ?", ct, start, end).ConfigureAwait(false),
                "warning", "Reconcile bank entries for accurate reporting."),
            new("unpaid_invoices", "Unpaid Sales Invoices",
                await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_sales_invoices` WHERE `status` = 'unpaid' AND `invoice_date` >= ? AND `invoice_date` <= ?", ct, start, end).ConfigureAwait(false),
                "info", "Outstanding invoices — OK to close but flagged for awareness."),
            new("pending_einvoice", "Pending E-Invoice Submissions",
                await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `status` IN ('draft','queued') AND `issue_date` >= ? AND `issue_date` <= ?", ct, start, end).ConfigureAwait(false),
                "warning", "Submit e-invoices to ASP before closing for FTA compliance."),
        };
        var snapshots = await CountAsync(connection, tx, "SELECT COUNT(*) FROM `epc_erp_inv_closing_snapshots` WHERE `period` = ? LIMIT 1", ct, yearMonth).ConfigureAwait(false);
        items.Add(new("inv_snapshot", "Inventory Period Snapshot", snapshots > 0 ? 0 : 1, "warning",
            snapshots > 0 ? "Inventory snapshot exists for this period." : "Run inventory period close to create a valuation snapshot."));
        return items;
    }

    /// <summary>PHP wraps each checklist query in try/catch — a missing table counts as 0.</summary>
    private static async Task<long> CountAsync(DbConnection connection, DbTransaction? tx, string sql, CancellationToken ct, params object?[] args)
    {
        try
        {
            return await ErpDb.LongAsync(connection, tx, ErpDb.Positional(sql), ct, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private async Task SetFiscalLockAsync(DbConnection connection, DbTransaction tx, long lockDate, string note, long adminId, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, tx, "UPDATE `epc_erp_fiscal_locks` SET `active` = 0 WHERE `active` = 1", ct).ConfigureAwait(false);
        if (lockDate > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("INSERT INTO `epc_erp_fiscal_locks` (`lock_date`, `note`, `admin_id`, `active`, `time_created`) VALUES (?, ?, ?, 1, ?)"),
                ct,
                lockDate,
                ErpFiscalSetLockWriteService.ClipNote(note),
                adminId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            connection, tx, (int)adminId, "fiscal_lock", "fiscal_period", lockDate,
            lockDate > 0 ? "Locked up to " + ErpFiscalSetLockWriteService.FormatYmd(lockDate) : "Cleared fiscal lock",
            null, ct).ConfigureAwait(false);
    }

    internal static async Task<string> EnsurePeriodAsync(DbConnection connection, DbTransaction? tx, string yearMonth, long now, CancellationToken ct)
    {
        var status = await ErpDb.StringAsync(
            connection, tx, ErpDb.Positional("SELECT `status` FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1"), ct, yearMonth).ConfigureAwait(false);
        if (status is not null)
        {
            return status;
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_periods` (`year_month`, `year`, `month`, `status`, `created_at`, `updated_at`) VALUES (?, ?, ?, 'open', ?, ?) ON DUPLICATE KEY UPDATE `year_month` = `year_month`"),
            ct,
            yearMonth,
            int.Parse(yearMonth[..4], CultureInfo.InvariantCulture),
            int.Parse(yearMonth[5..], CultureInfo.InvariantCulture),
            now,
            now).ConfigureAwait(false);
        return await ErpDb.StringAsync(
            connection, tx, ErpDb.Positional("SELECT `status` FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1"), ct, yearMonth).ConfigureAwait(false) ?? "open";
    }

    private static Task CloseLogAsync(DbConnection connection, DbTransaction tx, string yearMonth, string action, string oldStatus, string newStatus, long adminId, string note, long now, CancellationToken ct)
        => ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("INSERT INTO `epc_erp_period_close_log` (`year_month`, `action`, `old_status`, `new_status`, `admin_id`, `note`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
            ct,
            yearMonth, action, oldStatus, newStatus, adminId, note, now);

    internal static async Task<bool> SchemaReadyAsync(DbConnection connection, CancellationToken ct)
        => await ColumnExistsAsync(connection, "epc_erp_periods", "checklist_json", ct).ConfigureAwait(false)
           && await ColumnExistsAsync(connection, "epc_erp_period_close_log", "action", ct).ConfigureAwait(false)
           && await ColumnExistsAsync(connection, "epc_erp_fiscal_locks", "lock_date", ct).ConfigureAwait(false);

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken ct)
        => await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            ct, table, column).ConfigureAwait(false) > 0;
}
