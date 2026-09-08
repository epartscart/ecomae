namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>prja_budget_save</c> / <c>epc_prja_budget_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpPrjaBudgetSaveWriteService</c>.
/// </summary>
public interface IErpPrjaBudgetSaveDryRun
{
    ErpPrjaBudgetSaveDryRunResult Evaluate(ErpPrjaBudgetSaveRequest request);
}

public sealed class ErpPrjaBudgetSaveDryRun : IErpPrjaBudgetSaveDryRun
{
    public ErpPrjaBudgetSaveDryRunResult Evaluate(ErpPrjaBudgetSaveRequest request)
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

        var category = string.IsNullOrWhiteSpace(request.Category) ? (request.Code ?? "general") : request.Category;
        return new ErpPrjaBudgetSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.ProjectId, category,
            ["INSERT/UPDATE `epc_prja_budget` (NOT executed)"],
            "ErpPrjaBudgetSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_project_accounting.php");
    }

    private static ErpPrjaBudgetSaveDryRunResult Refuse(string s, string c, string d, ErpPrjaBudgetSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.ProjectId, r.Category ?? r.Code, [], d, "content/shop/finance/epc_erp_project_accounting.php");
}

public sealed record ErpPrjaBudgetSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    string? Category = null,
    string? Code = null,
    decimal CostBudget = 0,
    decimal RevenueBudget = 0,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpPrjaBudgetSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, long ProjectId, string? Category,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "prja_budget_save", id = Id, project_id = ProjectId, category = Category },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
