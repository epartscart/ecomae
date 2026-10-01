namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>gl_reverse_journal</c>.
/// Simulates reversing journal post only; never executes INSERT.
/// </summary>
public interface IErpGlReverseJournalDryRun
{
    Task<ErpGlReverseJournalDryRunResult> EvaluateAsync(
        ErpGlReverseJournalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpGlReverseJournalDryRun : IErpGlReverseJournalDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpGlReverseJournalDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpGlReverseJournalDryRunResult> EvaluateAsync(
        ErpGlReverseJournalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested; use the confirmed ASP.NET journal-reversal write path.",
                request);
        }

        if (request.JournalId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "journalId must be positive.", request);
        }

        var list = await _dashboards.ListErpGlJournalsAsync(200, cancellationToken);
        return EvaluateAgainstJournals(list.Journals, request);
    }

    public static ErpGlReverseJournalDryRunResult EvaluateAgainstJournals(
        IReadOnlyList<ErpGlJournalDigest> journals,
        ErpGlReverseJournalRequest request)
    {
        ArgumentNullException.ThrowIfNull(journals);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested; use the confirmed ASP.NET journal-reversal write path.",
                request);
        }

        if (request.JournalId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "journalId must be positive.", request);
        }

        var journal = journals.FirstOrDefault(j => j.Id == request.JournalId);
        if (journal is null)
        {
            return Refuse("dry-run-invalid", "journal_not_in_digest_window",
                $"Journal {request.JournalId} not present in recent /erp/gl-journals digest (active window).",
                request);
        }

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Reversal of {journal.JournalNo}"
            : request.Note.Trim();
        if (note.Length > 255)
        {
            note = note[..255];
        }

        return new ErpGlReverseJournalDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: false,
            ValidationCode: "ok",
            WouldWrite: true,
            JournalId: journal.Id,
            JournalNo: journal.JournalNo,
            TotalDebit: journal.TotalDebit,
            Note: note,
            SimulatedSql:
            [
                "SELECT lines FROM `epc_erp_gl_lines` WHERE journal_id=@id — swap debit/credit (NOT executed)",
                $"INSERT reversing journal reference='REV of {journal.JournalNo}' via epc_erp_gl_post_journal (NOT executed)",
                "Audit log gl_reverse is written by the confirmed ASP.NET reversal path (NOT executed)"
            ],
            Detail: "Journal found in digest window; reversal is validated without writing; confirmed ASP.NET posting swaps lines and records the audit event.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_reverse_journal");
    }

    private static ErpGlReverseJournalDryRunResult Refuse(
        string status, string code, string detail, ErpGlReverseJournalRequest request) =>
        new(status, 0, true, false, false, code, false, request.JournalId, null, null,
            request.Note, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_reverse_journal");
}

public sealed record ErpGlReverseJournalRequest(
    long JournalId,
    string? Note = null,
    bool ConfirmWrites = false);

public sealed record ErpGlReverseJournalDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long JournalId, string? JournalNo, decimal? TotalDebit,
    string? Note, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { journal_id = JournalId, note = Note },
        current = JournalNo is null ? null : new { journal_no = JournalNo, total_debit = TotalDebit },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
