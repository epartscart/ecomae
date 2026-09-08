namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bank_reconcile</c> / <c>epc_erp_bank_reconcile_match</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpBankReconcileWriteService</c>.
/// </summary>
public interface IErpBankReconcileDryRun
{
    ErpBankReconcileDryRunResult Evaluate(ErpBankReconcileRequest request);
}

public sealed class ErpBankReconcileDryRun : IErpBankReconcileDryRun
{
    public ErpBankReconcileDryRunResult Evaluate(ErpBankReconcileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                request);
        }

        return new ErpBankReconcileDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.LineId, request.EntryId,
            ["UPDATE `epc_erp_bank_statement_lines` (NOT executed)"],
            "ErpBankReconcile payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_phase8.php");
    }

    private static ErpBankReconcileDryRunResult Refuse(string s, string c, string d, ErpBankReconcileRequest r) =>
        new(s, 0, true, false, false, c, false, r.LineId, r.EntryId, [], d, "content/shop/finance/epc_erp_phase8.php");
}

public sealed record ErpBankReconcileRequest(bool ConfirmWrites = false, long LineId = 0, long EntryId = 0);

public sealed record ErpBankReconcileDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long LineId, long EntryId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "bank_reconcile", line_id = LineId, entry_id = EntryId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
