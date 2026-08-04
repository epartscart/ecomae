namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>cash_voucher_amend</c> (reference/note only).
/// Never executes UPDATE. PHP <c>ajax_erp.php</c> remains authoritative.
/// </summary>
public interface IErpCashVoucherAmendDryRun
{
    Task<ErpCashVoucherAmendDryRunResult> EvaluateAsync(
        ErpCashVoucherAmendRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCashVoucherAmendDryRun : IErpCashVoucherAmendDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpCashVoucherAmendDryRun(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<ErpCashVoucherAmendDryRunResult> EvaluateAsync(
        ErpCashVoucherAmendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET cash_voucher_amend is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.EntryId <= 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "entryId must be positive.",
                request);
        }

        var list = await _dashboards.ListErpCashEntriesAsync(null, 300, cancellationToken);
        return EvaluateAgainstEntries(list.Entries, request);
    }

    public static ErpCashVoucherAmendDryRunResult EvaluateAgainstEntries(
        IReadOnlyList<ErpCashEntryDigest> entries,
        ErpCashVoucherAmendRequest request)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET cash_voucher_amend is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.EntryId <= 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "entryId must be positive.",
                request);
        }

        var entry = entries.FirstOrDefault(e => e.Id == request.EntryId);
        if (entry is null)
        {
            return Refuse(
                "dry-run-invalid",
                "entry_not_in_digest_window",
                $"Cash entry {request.EntryId} not present in recent /erp/cash-entries digest window.",
                request);
        }

        var nextRef = request.Reference ?? entry.Reference;
        var nextNote = request.Note ?? entry.Note;
        var same = string.Equals(nextRef, entry.Reference, StringComparison.Ordinal)
                   && string.Equals(nextNote, entry.Note, StringComparison.Ordinal);

        if (same)
        {
            return new ErpCashVoucherAmendDryRunResult(
                Status: "dry-run-validated",
                Writes: 0,
                WritesBlocked: true,
                CutoverAllowed: false,
                PhpAuthoritative: true,
                ValidationCode: "no_change",
                WouldWrite: false,
                EntryId: entry.Id,
                AccountId: entry.AccountId,
                CurrentReference: entry.Reference,
                CurrentNote: entry.Note,
                IntendedReference: nextRef,
                IntendedNote: nextNote,
                SimulatedSql: null,
                Detail: "Reference/note unchanged — no UPDATE would run.",
                PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_voucher_amend");
        }

        return new ErpCashVoucherAmendDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            EntryId: entry.Id,
            AccountId: entry.AccountId,
            CurrentReference: entry.Reference,
            CurrentNote: entry.Note,
            IntendedReference: nextRef,
            IntendedNote: nextNote,
            SimulatedSql: "UPDATE `epc_erp_cash_bank_entries` SET `reference` = @reference, `note` = @note WHERE `id` = @entryId AND `active` = 1 (NOT executed)",
            Detail: "Narrative-only amend would be valid; amount/direction/posting untouched. Write blocked until dual-sample + approval.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_voucher_amend");
    }

    private static ErpCashVoucherAmendDryRunResult Refuse(
        string status,
        string validationCode,
        string detail,
        ErpCashVoucherAmendRequest request) =>
        new(
            Status: status,
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: false,
            EntryId: request.EntryId,
            AccountId: null,
            CurrentReference: null,
            CurrentNote: null,
            IntendedReference: request.Reference,
            IntendedNote: request.Note,
            SimulatedSql: null,
            Detail: detail,
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_voucher_amend");
}

public sealed record ErpCashVoucherAmendRequest(long EntryId, string? Reference, string? Note, bool ConfirmWrites = false);

public sealed record ErpCashVoucherAmendDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long EntryId,
    long? AccountId,
    string? CurrentReference,
    string? CurrentNote,
    string? IntendedReference,
    string? IntendedNote,
    string? SimulatedSql,
    string Detail,
    string PhpAjax)
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
        intended = new
        {
            entry_id = EntryId,
            reference = IntendedReference,
            note = IntendedNote
        },
        current = AccountId is null ? null : new
        {
            account_id = AccountId,
            reference = CurrentReference,
            note = CurrentNote
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
