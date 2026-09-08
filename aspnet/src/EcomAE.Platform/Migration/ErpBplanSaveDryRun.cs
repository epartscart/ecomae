namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bplan_save</c> / <c>epc_bplan_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpBplanSaveWriteService</c>.
/// </summary>
public interface IErpBplanSaveDryRun
{
    ErpBplanSaveDryRunResult Evaluate(ErpBplanSaveRequest request);
}

public sealed class ErpBplanSaveDryRun : IErpBplanSaveDryRun
{
    public ErpBplanSaveDryRunResult Evaluate(ErpBplanSaveRequest request)
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

        return new ErpBplanSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Name,
            ["UPDATE / INSERT `epc_bplan_plan` (NOT executed)"],
            "ErpBplanSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_budget_planning.php");
    }

    private static ErpBplanSaveDryRunResult Refuse(string s, string c, string d, ErpBplanSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Name, [], d, "content/shop/finance/epc_erp_budget_planning.php");
}

public sealed record ErpBplanSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    string? FiscalYear = null,
    string? Owner = null,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpBplanSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "bplan_save", id = Id, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
