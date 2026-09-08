using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>prj_task_save</c> / <c>epc_prj_task_save</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is <c>IErpPrjTaskSaveWriteService</c>.
/// </summary>
public interface IErpPrjTaskSaveDryRun
{
    ErpPrjTaskSaveDryRunResult Evaluate(ErpPrjTaskSaveRequest request);
}

public sealed class ErpPrjTaskSaveDryRun : IErpPrjTaskSaveDryRun
{
    public ErpPrjTaskSaveDryRunResult Evaluate(ErpPrjTaskSaveRequest request)
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

        if (request.ProjectId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select a project", request);
        }

        return new ErpPrjTaskSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.ProjectId, request.Name,
            ["INSERT/UPDATE `epc_prj_tasks` (NOT executed)"],
            "ErpPrjTaskSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_projects.php");
    }

    private static ErpPrjTaskSaveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPrjTaskSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.ProjectId, request.Name, [], detail,
            "content/shop/finance/epc_erp_projects.php");
}

public sealed record ErpPrjTaskSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    string? Name = null,
    bool ConfirmWrites = false);

public sealed record ErpPrjTaskSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, long ProjectId, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, projectId = ProjectId, name = Name, action = "prj_task_save" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
