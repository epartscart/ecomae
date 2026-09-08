namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>qm_plan_save</c> / <c>epc_qm_plan_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/UPSERT is
/// <c>IErpQmPlanSaveWriteService</c>.
/// </summary>
public interface IErpQmPlanSaveDryRun
{
    ErpQmPlanSaveDryRunResult Evaluate(ErpQmPlanSaveRequest request);
}

public sealed class ErpQmPlanSaveDryRun : IErpQmPlanSaveDryRun
{
    public ErpQmPlanSaveDryRunResult Evaluate(ErpQmPlanSaveRequest request)
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

        return new ErpQmPlanSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPDATE / INSERT `epc_qm_plan` ON DUPLICATE KEY UPDATE (NOT executed)"],
            "ErpQmPlanSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_quality.php");
    }

    private static ErpQmPlanSaveDryRunResult Refuse(string s, string c, string d, ErpQmPlanSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_quality.php");
}

public sealed record ErpQmPlanSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpQmPlanSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "qm_plan_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
