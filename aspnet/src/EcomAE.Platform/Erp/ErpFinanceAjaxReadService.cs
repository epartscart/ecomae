using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twins of the read-only ajax_erp.php cases <c>period_log</c>
/// (<c>epc_erp_period_close_log_list</c>), <c>settlement_open_docs</c>
/// (<c>epc_erp_open_customer_invoices</c> / <c>epc_erp_open_supplier_bills</c>) and
/// <c>invoice_list</c> (<c>epc_erp_invoice_list</c>). Rows are projected with the PHP column names.
/// Never creates schema; unprovisioned PHP-owned tables fail closed.
/// </summary>
public interface IErpFinanceAjaxReadService
{
    Task<ErpAjaxRowsResult> PeriodLogAsync(string? yearMonth, CancellationToken cancellationToken = default);

    Task<ErpAjaxRowsResult> SettlementOpenDocsAsync(string? docType, long counterpartyId, CancellationToken cancellationToken = default);

    Task<ErpAjaxRowsResult> InvoiceListAsync(string? from, string? to, string? status, string? q, CancellationToken cancellationToken = default);
}

public sealed record ErpAjaxRowsResult(ErpSimpleWriteResult Result, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

public sealed class ErpFinanceAjaxReadService : IErpFinanceAjaxReadService
{
    public const int PeriodLogLimit = 50;
    public const int InvoiceListLimit = 200;
    public const string InvoicesNotProvisioned = "E-invoice tables are not provisioned";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpFinanceAjaxReadService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>PHP: <c>$sdType === 'ap'</c> selects supplier bills; anything else is AR.</summary>
    public static bool IsPayables(string? docType) => string.Equals(docType, "ap", StringComparison.Ordinal);

    /// <summary>PHP: <c>strtotime($from . ' 00:00:00')</c>, default <c>date('Y-m-01')</c>.</summary>
    public static long FromUnix(string? from, DateTimeOffset now)
    {
        if (!string.IsNullOrEmpty(from) && TryDate(from, out var d))
        {
            return d.ToUnixTimeSeconds();
        }

        return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    /// <summary>PHP: <c>strtotime($to . ' 23:59:59')</c>, default <c>time()</c>.</summary>
    public static long ToUnix(string? to, DateTimeOffset now)
    {
        if (!string.IsNullOrEmpty(to) && TryDate(to, out var d))
        {
            return d.AddDays(1).AddSeconds(-1).ToUnixTimeSeconds();
        }

        return now.ToUnixTimeSeconds();
    }

    /// <summary>PHP: <c>ctype_digit($q) ? (int)$q : 0</c> for the order_id equality match.</summary>
    public static long OrderIdFilter(string q)
        => q.Length > 0 && q.All(char.IsAsciiDigit) && long.TryParse(q, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;

    private static bool TryDate(string raw, out DateTimeOffset value)
    {
        if (DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
        {
            value = new DateTimeOffset(d, TimeSpan.Zero);
            return true;
        }

        value = default;
        return false;
    }

    public async Task<ErpAjaxRowsResult> PeriodLogAsync(string? yearMonth, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpPeriodLockReopenWriteService.SchemaReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return Fail("invalid", ErpPeriodReadService.NotProvisioned);
        }

        var ym = yearMonth ?? string.Empty;
        var rows = ym.Length > 0
            ? await RowsAsync(connection, ErpDb.Positional("SELECT * FROM `epc_erp_period_close_log` WHERE `year_month` = ? ORDER BY `created_at` DESC LIMIT " + PeriodLogLimit.ToString(CultureInfo.InvariantCulture)), cancellationToken, ym).ConfigureAwait(false)
            : await RowsAsync(connection, "SELECT * FROM `epc_erp_period_close_log` ORDER BY `created_at` DESC LIMIT " + PeriodLogLimit.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    public async Task<ErpAjaxRowsResult> SettlementOpenDocsAsync(string? docType, long counterpartyId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        if (counterpartyId <= 0)
        {
            return Ok([]);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (IsPayables(docType))
        {
            if (!await TableExistsAsync(connection, "epc_erp_purchases", cancellationToken).ConfigureAwait(false))
            {
                return Ok([]);
            }

            const string paid = "IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0)";
            return Ok(await RowsAsync(
                connection,
                ErpDb.Positional(
                    "SELECT p.`id`, p.`invoice_number`, p.`purchase_date`, p.`total_amount`, " + paid + " AS paid,"
                    + " ROUND(p.`total_amount` - " + paid + ", 2) AS outstanding"
                    + " FROM `epc_erp_purchases` p"
                    + " WHERE p.`active` = 1 AND p.`status` <> 'draft' AND p.`supplier_id` = ?"
                    + " HAVING outstanding > 0.005"
                    + " ORDER BY p.`purchase_date` ASC, p.`id` ASC"),
                cancellationToken,
                counterpartyId).ConfigureAwait(false));
        }

        if (!await TableExistsAsync(connection, "epc_einvoice_documents", cancellationToken).ConfigureAwait(false))
        {
            return Ok([]);
        }

        return Ok(await RowsAsync(
            connection,
            ErpDb.Positional(
                "SELECT `id`, `invoice_number`, `issue_date`, `payment_due_date`, `total_incl_vat`, `paid_amount`,"
                + " ROUND(`total_incl_vat` - `paid_amount`, 2) AS outstanding"
                + " FROM `epc_einvoice_documents`"
                + " WHERE `active` = 1 AND `status` <> 'cancelled'"
                + " AND `doc_category` IN ('tax_invoice','commercial_invoice') AND `user_id` = ?"
                + " AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005"
                + " ORDER BY (CASE WHEN `payment_due_date` > 0 THEN `payment_due_date` ELSE `issue_date` END) ASC, `id` ASC"),
            cancellationToken,
            counterpartyId).ConfigureAwait(false));
    }

    public async Task<ErpAjaxRowsResult> InvoiceListAsync(string? from, string? to, string? status, string? q, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_einvoice_documents", cancellationToken).ConfigureAwait(false))
        {
            return Fail("invalid", InvoicesNotProvisioned);
        }

        var now = _clock.GetUtcNow();
        var sql = "SELECT d.*, u.`email` AS customer_email FROM `epc_einvoice_documents` d LEFT JOIN `users` u ON u.`user_id` = d.`user_id` WHERE d.`active` = 1 AND d.`issue_date` >= ? AND d.`issue_date` <= ?";
        var parameters = new List<object?> { FromUnix(from, now), ToUnix(to, now) };
        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND d.`status` = ?";
            parameters.Add(status);
        }

        if (!string.IsNullOrEmpty(q))
        {
            var trimmed = q.Trim();
            sql += " AND (d.`invoice_number` LIKE ? OR d.`order_id` = ?)";
            parameters.Add("%" + trimmed + "%");
            parameters.Add(OrderIdFilter(trimmed));
        }

        sql += " ORDER BY d.`issue_date` DESC, d.`id` DESC LIMIT " + InvoiceListLimit.ToString(CultureInfo.InvariantCulture);
        return Ok(await RowsAsync(connection, ErpDb.Positional(sql), cancellationToken, parameters.ToArray()).ConfigureAwait(false));
    }

    private static ErpAjaxRowsResult Ok(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
        => new(ErpSimpleWriteResult.Ok("OK", 0) with { Writes = 0 }, rows);

    private static ErpAjaxRowsResult Fail(string code, string message)
        => new(ErpSimpleWriteResult.Fail(code, message), []);

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken ct)
        => await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            ct, table).ConfigureAwait(false) > 0;

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RowsAsync(DbConnection connection, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, parameters);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var map = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                map[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(map);
        }

        return rows;
    }
}
