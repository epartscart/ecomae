namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>gl_manual_entry</c> / <c>epc_erp_gl_post_journal</c>.
/// Validates balanced lines + COA ids from digest; never INSERTs.
/// </summary>
public interface IErpGlManualEntryDryRun
{
    Task<ErpGlManualEntryDryRunResult> EvaluateAsync(
        ErpGlManualEntryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpGlManualEntryDryRun : IErpGlManualEntryDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpGlManualEntryDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpGlManualEntryDryRunResult> EvaluateAsync(
        ErpGlManualEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET gl_manual_entry is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var coa = await _dashboards.ListErpCoaAccountsAsync(500, cancellationToken);
        return EvaluateAgainstCoa(coa.Accounts, request);
    }

    public static ErpGlManualEntryDryRunResult EvaluateAgainstCoa(
        IReadOnlyList<ErpCoaAccountDigest> accounts,
        ErpGlManualEntryRequest request)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET gl_manual_entry is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var lines = request.Lines ?? [];
        if (lines.Count < 2)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Add at least two GL lines (PHP).", request);
        }

        var debit = lines.Sum(l => l.Debit);
        var credit = lines.Sum(l => l.Credit);
        if (Math.Round(debit, 2) != Math.Round(credit, 2) || debit <= 0)
        {
            return Refuse("dry-run-invalid", "unbalanced",
                $"Debits ({debit}) must equal credits ({credit}) and be > 0.", request);
        }

        var activeIds = accounts.Where(a => a.Active).Select(a => a.Id).ToHashSet();
        foreach (var line in lines)
        {
            if (line.CoaId <= 0 || !activeIds.Contains(line.CoaId))
            {
                return Refuse("dry-run-invalid", "coa_not_found",
                    $"COA id {line.CoaId} missing from active /erp/coa-accounts digest window.", request);
            }

            if (line.Debit < 0 || line.Credit < 0 || (line.Debit > 0 && line.Credit > 0))
            {
                return Refuse("dry-run-invalid", "invalid_line",
                    "Each line must have non-negative debit XOR credit.", request);
            }
        }

        return new ErpGlManualEntryDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            LineCount: lines.Count,
            DebitTotal: debit,
            CreditTotal: credit,
            Reference: request.Reference ?? "",
            Description: string.IsNullOrWhiteSpace(request.Description) ? "Manual journal entry" : request.Description!,
            SimulatedSql:
            [
                "INSERT INTO `epc_erp_gl_journals` (…, source_type='manual', …) (NOT executed)",
                "INSERT INTO `epc_erp_gl_lines` (journal_id, coa_id, debit, credit, line_note) × N (NOT executed)"
            ],
            Detail: "Balanced manual journal would post under PHP rules; INSERT blocked until dual-sample + approval.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_manual_entry");
    }

    private static ErpGlManualEntryDryRunResult Refuse(
        string status, string code, string detail, ErpGlManualEntryRequest request) =>
        new(status, 0, true, false, true, code, false, request.Lines?.Count ?? 0, 0, 0,
            request.Reference ?? "", request.Description ?? "", [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_manual_entry");
}

public sealed record ErpGlManualLine(long CoaId, decimal Debit, decimal Credit, string? LineNote = null);

public sealed record ErpGlManualEntryRequest(
    IReadOnlyList<ErpGlManualLine>? Lines,
    string? Reference = null,
    string? Description = null,
    bool ConfirmWrites = false);

public sealed record ErpGlManualEntryDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, int LineCount, decimal DebitTotal, decimal CreditTotal,
    string Reference, string Description, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
            line_count = LineCount,
            debit_total = DebitTotal,
            credit_total = CreditTotal,
            reference = Reference,
            description = Description,
            source_type = "manual"
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
