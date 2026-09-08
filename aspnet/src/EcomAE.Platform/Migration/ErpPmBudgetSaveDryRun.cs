namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pm_budget_save</c> / <c>epc_erp_pm_budget_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpPmBudgetSaveWriteService</c>.
/// </summary>
public interface IErpPmBudgetSaveDryRun
{
    ErpPmBudgetSaveDryRunResult Evaluate(ErpPmBudgetSaveRequest request);
}

public sealed class ErpPmBudgetSaveDryRun : IErpPmBudgetSaveDryRun
{
    public ErpPmBudgetSaveDryRunResult Evaluate(ErpPmBudgetSaveRequest request)
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

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "id must be >= 0.", request);
        }

        return new ErpPmBudgetSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["ajax_erp.php?action=pm_budget_save (NOT executed)"],
            "ErpPmBudgetSave payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=pm_budget_save");
    }

    private static ErpPmBudgetSaveDryRunResult Refuse(string s, string c, string d, ErpPmBudgetSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=pm_budget_save");
}

public sealed record ErpPmBudgetSaveRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpPmBudgetSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "pm_budget_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
