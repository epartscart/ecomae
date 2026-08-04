namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>cash_voucher_void</c>.
/// Simulates soft-void UPDATE only; GL reverse/settlement unwind stay PHP-authoritative.
/// </summary>
public interface IErpCashVoucherVoidDryRun
{
    Task<ErpCashVoucherVoidDryRunResult> EvaluateAsync(
        ErpCashVoucherVoidRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCashVoucherVoidDryRun : IErpCashVoucherVoidDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpCashVoucherVoidDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpCashVoucherVoidDryRunResult> EvaluateAsync(
        ErpCashVoucherVoidRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET cash_voucher_void is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.EntryId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "entryId must be positive.", request);
        }

        var list = await _dashboards.ListErpCashEntriesAsync(null, 300, cancellationToken);
        return EvaluateAgainstEntries(list.Entries, request);
    }

    public static ErpCashVoucherVoidDryRunResult EvaluateAgainstEntries(
        IReadOnlyList<ErpCashEntryDigest> entries,
        ErpCashVoucherVoidRequest request)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET cash_voucher_void is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.EntryId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "entryId must be positive.", request);
        }

        var entry = entries.FirstOrDefault(e => e.Id == request.EntryId);
        if (entry is null)
        {
            return Refuse("dry-run-invalid", "entry_not_in_digest_window",
                $"Cash entry {request.EntryId} not present in recent /erp/cash-entries digest (active=1 window).",
                request);
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Voided by operator" : request.Reason.Trim();
        return new ErpCashVoucherVoidDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            EntryId: entry.Id,
            AccountId: entry.AccountId,
            Amount: entry.Amount,
            Reason: reason.Length > 255 ? reason[..255] : reason,
            SimulatedSql:
            [
                "UPDATE `epc_erp_cash_bank_entries` SET `active`=0, `voided_at`=@now, `void_reason`=@reason, `voided_by`=@admin, `reversal_journal_id`=@rev WHERE `id`=@id (NOT executed)",
                "GL reverse journals + settlement unwind remain PHP-only in this dry-run slice"
            ],
            Detail: "Entry found in active digest window; soft-void UPDATE simulated. GL reverse/settlements stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_voucher_void");
    }

    private static ErpCashVoucherVoidDryRunResult Refuse(
        string status, string code, string detail, ErpCashVoucherVoidRequest request) =>
        new(status, 0, true, false, true, code, false, request.EntryId, null, null,
            request.Reason, [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_voucher_void");
}

public sealed record ErpCashVoucherVoidRequest(long EntryId, string? Reason = null, bool ConfirmWrites = false);

public sealed record ErpCashVoucherVoidDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long EntryId, long? AccountId, decimal? Amount,
    string? Reason, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { entry_id = EntryId, reason = Reason },
        current = AccountId is null ? null : new { account_id = AccountId, amount = Amount },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
