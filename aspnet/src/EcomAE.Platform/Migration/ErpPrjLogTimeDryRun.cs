using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>prj_log_time</c> / <c>epc_prj_log_time</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT is <c>IErpPrjLogTimeWriteService</c>.
/// </summary>
public interface IErpPrjLogTimeDryRun
{
    ErpPrjLogTimeDryRunResult Evaluate(ErpPrjLogTimeRequest request);
}

public sealed class ErpPrjLogTimeDryRun : IErpPrjLogTimeDryRun
{
    public ErpPrjLogTimeDryRunResult Evaluate(ErpPrjLogTimeRequest request)
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

        return new ErpPrjLogTimeDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.ProjectId, request.TaskId, request.Hours,
            ["INSERT `epc_prj_timesheets` (NOT executed)"],
            "ErpPrjLogTime payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_projects.php");
    }

    private static ErpPrjLogTimeDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPrjLogTimeRequest request) =>
        new(status, 0, true, false, false, code, false, request.ProjectId, request.TaskId, request.Hours, [], detail,
            "content/shop/finance/epc_erp_projects.php");
}

public sealed record ErpPrjLogTimeRequest(
    long ProjectId = 0,
    long TaskId = 0,
    decimal Hours = 0,
    bool ConfirmWrites = false);

public sealed record ErpPrjLogTimeDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ProjectId, long TaskId, decimal Hours,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { projectId = ProjectId, taskId = TaskId, hours = Hours, action = "prj_log_time" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
